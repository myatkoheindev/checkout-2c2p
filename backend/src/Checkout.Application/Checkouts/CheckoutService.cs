using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Checkouts.DTOs;
using Checkout.Application.Common;
using Checkout.Application.Configuration;
using Checkout.Application.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Checkout.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Checkout.Application.Checkouts;

/// <summary>Default <see cref="ICheckoutService"/>.</summary>
/// <remarks>
/// Three guards stop a double charge, each covering the one before it:
/// <list type="number">
/// <item><b>Redis lock</b> per idempotency key: concurrent requests with one key run one at a
/// time, across every API instance. A request that finds it held waits for the first to finish
/// and then replays its result, or fails with <see cref="ErrorCodes.CheckoutInProgress"/> after
/// <see cref="CheckoutOptions.LockWait"/>.</item>
/// <item><b>Replay</b>: once a transaction exists for the key, its stored invoice number and
/// redirect URL are returned and the payment provider is not called again.</item>
/// <item><b>MySQL unique index</b> on <c>idempotency_key</c>: if the lock expired or Redis is
/// down, a second insert for the same key fails, and that request replays instead.</item>
/// </list>
/// </remarks>
public sealed partial class CheckoutService(
    IProductRepository products,
    ICustomerRepository customers,
    ICustomerCardRepository cards,
    ITransactionRepository transactions,
    IUnitOfWork unitOfWork,
    IPaymentGateway gateway,
    IPaymentConfirmationService confirmation,
    IDistributedLock distributedLock,
    IInvoiceNumberGenerator invoiceNumbers,
    IValidator<CheckoutCommand> validator,
    IOptions<CheckoutOptions> checkoutOptions,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    /// <summary>Recorded as <c>resp_code</c> when the payment provider could not be reached to open a session.</summary>
    public const string GatewayErrorCode = "GATEWAY";

    /// <summary>The path of the backend notification endpoint, appended to the public API URL.</summary>
    public const string NotificationPath = "/api/payments/2c2p/notification";

    /// <summary>The frontend route the payment provider returns the browser to.</summary>
    public const string FrontendReturnPath = "/payment/return";

    private CheckoutOptions Options => checkoutOptions.Value;

    /// <inheritdoc />
    public async Task<Result<CheckoutResult>> CheckoutAsync(
        CheckoutCommand request,
        string idempotencyKey,
        string? clientIp,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var validation = await validator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return Result<CheckoutResult>.ValidationFailure(new Dictionary<string, string[]>(validation.ToDictionary()));
        }

        var product = await products.GetByIdAsync(request.ProductId, ct).ConfigureAwait(false);
        if (product is null)
        {
            return Result<CheckoutResult>.Failure($"Product {request.ProductId} does not exist.", ErrorCodes.ProductNotFound);
        }

        if (!await customers.ExistsAsync(request.CustomerId, ct).ConfigureAwait(false))
        {
            return Result<CheckoutResult>.Failure($"Customer {request.CustomerId} does not exist.", ErrorCodes.CustomerNotFound);
        }

        string? savedCardToken = null;
        if (request.CustomerCardId is { } cardId)
        {
            // A card id that belongs to another customer is answered exactly like a missing one.
            var card = await cards.GetForCustomerAsync(cardId, request.CustomerId, ct).ConfigureAwait(false);
            if (card is null)
            {
                return Result<CheckoutResult>.Failure(
                    $"Card {cardId} is not a saved card of customer {request.CustomerId}.",
                    ErrorCodes.CardNotFound);
            }

            savedCardToken = card.CustomerToken;
        }

        var requestHash = HashOf(request);

        var lease = await AcquireLockAsync(idempotencyKey, ct).ConfigureAwait(false);
        await using var leaseScope = lease.ConfigureAwait(false);
        if (lease.Status == LockStatus.Held)
        {
            LogStillInProgress(idempotencyKey);
            return InProgress();
        }

        if (lease.Status == LockStatus.Unavailable)
        {
            LogLockUnavailable(idempotencyKey);
        }

        // Inside the lock: an earlier request with this key may have finished while we waited.
        var existing = await transactions.GetByIdempotencyKeyAsync(idempotencyKey, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, requestHash);
        }

        var transaction = await InsertPendingAsync(request, product, idempotencyKey, requestHash, ct).ConfigureAwait(false);
        if (transaction is null)
        {
            // The unique index caught a concurrent insert for this key (lock expired or Redis down).
            existing = await transactions.GetByIdempotencyKeyAsync(idempotencyKey, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Insert for key {idempotencyKey} failed, but no transaction holds it.");
            return Replay(existing, requestHash);
        }

        await StartPaymentAsync(transaction, product, savedCardToken, clientIp, ct).ConfigureAwait(false);

        var saved = await transactions.GetByInvoiceNoAsync(transaction.InvoiceNo, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Transaction {transaction.InvoiceNo} disappeared.");

        return Result<CheckoutResult>.Success(new CheckoutResult(saved.InvoiceNo, saved.Status, saved.RedirectUrl));
    }

    /// <summary>
    /// Takes the lock for this key, retrying while another request holds it, up to
    /// <see cref="CheckoutOptions.LockWait"/>.
    /// </summary>
    private async Task<LockLease> AcquireLockAsync(string idempotencyKey, CancellationToken ct)
    {
        var resource = $"checkout:lock:{idempotencyKey}";
        var deadline = timeProvider.GetUtcNow() + Options.LockWait;

        while (true)
        {
            var lease = await distributedLock.TryAcquireAsync(resource, Options.LockTtl, ct).ConfigureAwait(false);
            if (lease.Status != LockStatus.Held || timeProvider.GetUtcNow() >= deadline)
            {
                return lease;
            }

            await Task.Delay(Options.LockPollInterval, timeProvider, ct).ConfigureAwait(false);
        }
    }

    private Result<CheckoutResult> Replay(PaymentTransaction existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            LogKeyReused(existing.IdempotencyKey, existing.InvoiceNo);
            return Result<CheckoutResult>.Failure(
                "This Idempotency-Key was already used for a different checkout. Generate a new key for a new purchase.",
                ErrorCodes.IdempotencyKeyReused);
        }

        // Pending with no session and no response yet: the first request is still talking to the provider.
        // Reached only when the lock did not serialize us (expired, or Redis down).
        if (existing.Status == TransactionStatus.Pending && existing.PaymentToken is null && existing.RespCode is null)
        {
            return InProgress();
        }

        LogReplayed(existing.IdempotencyKey, existing.InvoiceNo);
        return Result<CheckoutResult>.Success(
            new CheckoutResult(existing.InvoiceNo, existing.Status, existing.RedirectUrl) { Replayed = true });
    }

    private static Result<CheckoutResult> InProgress() =>
        Result<CheckoutResult>.Failure(
            "A checkout with this Idempotency-Key is still in progress. Retry shortly with the same key.",
            ErrorCodes.CheckoutInProgress);

    /// <summary>
    /// Inserts the Pending transaction. Returns <c>null</c> when the idempotency key is already
    /// taken; retries once with a new invoice number on the (astronomically rare) invoice clash.
    /// </summary>
    private async Task<PaymentTransaction?> InsertPendingAsync(
        CheckoutCommand request,
        Product product,
        string idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var transaction = PaymentTransaction.StartPending(
                request.CustomerId,
                product,
                request.CustomerCardId,
                invoiceNumbers.NewInvoiceNo(),
                idempotencyKey,
                requestHash,
                timeProvider.GetUtcNow().UtcDateTime);

            switch (await transactions.TryAddAsync(transaction, ct).ConfigureAwait(false))
            {
                case TransactionInsertResult.Added:
                    LogCreated(transaction.InvoiceNo, idempotencyKey, transaction.Flow);
                    return transaction;
                case TransactionInsertResult.DuplicateIdempotencyKey:
                    LogDuplicateInsertBlocked(idempotencyKey);
                    return null;
                case TransactionInsertResult.DuplicateInvoiceNo:
                    continue;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique invoice number.");
    }

    /// <summary>Opens a payment session with the provider and records what it answered.</summary>
    private async Task StartPaymentAsync(
        PaymentTransaction transaction,
        Product product,
        string? savedCardToken,
        string? clientIp,
        CancellationToken ct)
    {
        var returnUrl = FrontendReturnUrl(transaction.InvoiceNo);
        var request = PaymentSessionRequest.ForCheckout(
            product.PaymentType,
            transaction.InvoiceNo,
            product.Name,
            transaction.Amount,
            transaction.Currency,
            returnUrl,
            Options.PublicApiUrl.TrimEnd('/') + NotificationPath,
            savedCardToken);

        PaymentSession session;
        try
        {
            session = await gateway.CreateSessionAsync(request, ct).ConfigureAwait(false);
        }
        catch (PaymentGatewayException exception)
        {
            // No session reached the customer, so no payment can follow: safe to fail now.
            LogGatewayFailed(transaction.InvoiceNo, "create session", exception);
            await confirmation.MarkFailedAsync(transaction.InvoiceNo, GatewayErrorCode, "The payment provider is unavailable. Please try again.", ct).ConfigureAwait(false);
            return;
        }

        if (!session.IsCreated)
        {
            await confirmation.MarkFailedAsync(transaction.InvoiceNo, session.ResponseCode, session.ResponseMessage, ct).ConfigureAwait(false);
            return;
        }

        if (savedCardToken is null)
        {
            // Hosted page: the browser goes to the provider; its notification settles the payment.
            transaction.AttachPaymentToken(session.SessionId!, session.RedirectUrl, timeProvider.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        // Saved before charging, so a charge that times out leaves a transaction whose status can be checked.
        transaction.AttachPaymentToken(session.SessionId!, redirectUrl: null, timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        await ChargeSavedCardAsync(transaction, session.SessionId!, savedCardToken, returnUrl, clientIp, ct).ConfigureAwait(false);
    }

    /// <summary>Charges the saved card, then checks the status: a saved card is always confirmed by a status check.</summary>
    private async Task ChargeSavedCardAsync(
        PaymentTransaction transaction,
        string sessionId,
        string savedCardToken,
        string returnUrl,
        string? clientIp,
        CancellationToken ct)
    {
        SavedCardCharge charge;
        try
        {
            charge = await gateway.ChargeSavedCardAsync(new SavedCardChargeRequest(sessionId, savedCardToken, returnUrl, clientIp), ct).ConfigureAwait(false);
        }
        catch (PaymentGatewayException exception)
        {
            // The charge may or may not have happened. Leave it Pending; the result page's
            // status check finds out.
            LogGatewayFailed(transaction.InvoiceNo, "charge saved card", exception);
            return;
        }

        switch (charge.Outcome)
        {
            case ChargeOutcome.RedirectRequired:
                transaction.SetRedirect(charge.RedirectUrl!, timeProvider.GetUtcNow().UtcDateTime);
                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                return;

            case ChargeOutcome.Declined:
                await confirmation.MarkFailedAsync(transaction.InvoiceNo, charge.ResponseCode, charge.ResponseMessage, ct).ConfigureAwait(false);
                return;

            case ChargeOutcome.Submitted:
                // Submitted only says the provider finished; the status check says whether it succeeded.
                try
                {
                    var result = await gateway.GetPaymentStatusAsync(transaction.InvoiceNo, ct).ConfigureAwait(false);
                    await confirmation.ApplyAsync(result, ct).ConfigureAwait(false);
                }
                catch (PaymentGatewayException exception)
                {
                    LogGatewayFailed(transaction.InvoiceNo, "status check", exception);
                }

                return;
        }
    }

    private string FrontendReturnUrl(string invoiceNo) =>
        $"{frontendOptions.Value.Url.TrimEnd('/')}{FrontendReturnPath}?invoiceNo={Uri.EscapeDataString(invoiceNo)}";

    /// <summary>SHA-256 of the request body, to tell a retry from a different purchase under the same key.</summary>
    public static string HashOf(CheckoutCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"customer={request.CustomerId};product={request.ProductId};card={request.CustomerCardId?.ToString(CultureInfo.InvariantCulture) ?? "-"}");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {InvoiceNo} for key {IdempotencyKey} ({Flow}).")]
    private partial void LogCreated(string invoiceNo, string idempotencyKey, PaymentFlow flow);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replayed {InvoiceNo} for key {IdempotencyKey}; payment provider not called.")]
    private partial void LogReplayed(string idempotencyKey, string invoiceNo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Key {IdempotencyKey} reused with a different request (original {InvoiceNo}).")]
    private partial void LogKeyReused(string idempotencyKey, string invoiceNo);

    [LoggerMessage(Level = LogLevel.Information, Message = "Key {IdempotencyKey} still in progress after waiting; answering 409.")]
    private partial void LogStillInProgress(string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis lock unavailable for key {IdempotencyKey}; relying on the MySQL unique index.")]
    private partial void LogLockUnavailable(string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unique index blocked a second transaction for key {IdempotencyKey}.")]
    private partial void LogDuplicateInsertBlocked(string idempotencyKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment provider {Step} failed for {InvoiceNo}.")]
    private partial void LogGatewayFailed(string invoiceNo, string step, Exception exception);
}
