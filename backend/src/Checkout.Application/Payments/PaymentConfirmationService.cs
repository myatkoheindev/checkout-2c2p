using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Checkout.Application.Payments;

/// <summary>Default <see cref="IPaymentConfirmationService"/>.</summary>
/// <remarks>
/// Every status change is a single conditional update (<c>WHERE status = 'Pending'</c>) in
/// <see cref="ITransactionRepository"/>, so the provider notification, a status check, and a
/// retried notification can race safely: exactly one wins, and a final status is never overwritten.
/// </remarks>
public sealed partial class PaymentConfirmationService(
    ITransactionRepository transactions,
    ICustomerCardRepository cards,
    ICacheService cache,
    TimeProvider timeProvider,
    ILogger<PaymentConfirmationService> logger) : IPaymentConfirmationService
{
    /// <inheritdoc />
    public async Task<ConfirmationOutcome> ApplyAsync(PaymentResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var transaction = await transactions.GetByInvoiceNoAsync(result.InvoiceNo, ct).ConfigureAwait(false);
        if (transaction is null)
        {
            LogUnknownInvoice(result.InvoiceNo, result.ResponseCode);
            return ConfirmationOutcome.UnknownInvoice;
        }

        if (result.Status == PaymentStatus.Pending)
        {
            return ConfirmationOutcome.NoOutcomeYet;
        }

        var succeeded = result.Status == PaymentStatus.Succeeded;
        if (succeeded && (result.Amount != transaction.Amount
            || !string.Equals(result.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase)))
        {
            // Never mark paid on numbers that disagree; leave it Pending for someone to look at.
            LogAmountMismatch(result.InvoiceNo, transaction.Amount, transaction.Currency, result.Amount, result.Currency);
            return ConfirmationOutcome.AmountMismatch;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Saved even when the status is already final: a repeated notification then retries a
        // card save that failed the first time, and the unique index stops a duplicate.
        if (succeeded && result.SavedCardToken is not null)
        {
            var card = new CustomerCard(
                transaction.CustomerId,
                Truncate(result.SavedCardToken, 100)!,
                Truncate(result.MaskedCardNo, 30) ?? string.Empty,
                now);

            if (await cards.AddIfMissingAsync(card, ct).ConfigureAwait(false))
            {
                LogCardSaved(transaction.CustomerId, card.MaskedCardNo);
                await cache.RemoveAsync(CacheKeys.CustomerCards(transaction.CustomerId)).ConfigureAwait(false);
            }
        }

        var outcome = new PaymentOutcome(
            Truncate(result.ResponseCode, 10)!,
            Truncate(result.ResponseMessage, 100),
            Truncate(result.PaymentMethod, 20),
            Truncate(result.MaskedCardNo, 30),
            Truncate(result.ProviderReference, 50),
            now);

        var changed = succeeded
            ? await transactions.MarkSucceededIfPendingAsync(transaction.Id, outcome, ct).ConfigureAwait(false)
            : await transactions.MarkFailedIfPendingAsync(transaction.InvoiceNo, outcome, ct).ConfigureAwait(false);

        if (!changed)
        {
            return ConfirmationOutcome.AlreadyFinal;
        }

        LogApplied(result.InvoiceNo, succeeded ? TransactionStatus.Succeeded : TransactionStatus.Failed, result.ResponseCode);
        return ConfirmationOutcome.Applied;
    }

    /// <inheritdoc />
    public async Task<bool> MarkFailedAsync(string invoiceNo, string respCode, string? respDesc, CancellationToken ct = default)
    {
        var outcome = new PaymentOutcome(
            Truncate(respCode, 10)!,
            Truncate(respDesc, 100),
            PaymentChannel: null,
            MaskedCardNo: null,
            ProviderRef: null,
            timeProvider.GetUtcNow().UtcDateTime);

        var changed = await transactions.MarkFailedIfPendingAsync(invoiceNo, outcome, ct).ConfigureAwait(false);
        if (changed)
        {
            LogApplied(invoiceNo, TransactionStatus.Failed, respCode);
        }

        return changed;
    }

    private static string? Truncate(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];

    [LoggerMessage(Level = LogLevel.Information, Message = "Transaction {InvoiceNo} is now {Status} ({RespCode}).")]
    private partial void LogApplied(string invoiceNo, TransactionStatus status, string respCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment result for unknown invoice {InvoiceNo} ({RespCode}) ignored.")]
    private partial void LogUnknownInvoice(string invoiceNo, string respCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Payment success for {InvoiceNo} does not match: stored {StoredAmount} {StoredCurrency}, received {Amount} {Currency}. Left Pending.")]
    private partial void LogAmountMismatch(string invoiceNo, decimal storedAmount, string storedCurrency, decimal amount, string currency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved card {MaskedCardNo} for customer {CustomerId}.")]
    private partial void LogCardSaved(int customerId, string maskedCardNo);
}
