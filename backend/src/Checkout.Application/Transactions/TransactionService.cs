using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Common;
using Checkout.Application.Configuration;
using Checkout.Application.Payments;
using Checkout.Application.Transactions.DTOs;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Checkout.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Checkout.Application.Transactions;

/// <summary>Default <see cref="ITransactionService"/>.</summary>
public sealed partial class TransactionService(
    ITransactionRepository transactions,
    IPaymentGateway gateway,
    IPaymentConfirmationService confirmation,
    IDistributedLock distributedLock,
    IOptions<CheckoutOptions> options,
    ILogger<TransactionService> logger) : ITransactionService
{
    /// <inheritdoc />
    public async Task<Result<TransactionDetails>> GetAsync(string invoiceNo, CancellationToken ct = default)
    {
        var transaction = await transactions.GetWithDetailsAsync(invoiceNo, ct).ConfigureAwait(false);

        return transaction is null
            ? NotFound(invoiceNo)
            : Result<TransactionDetails>.Success(ToDetails(transaction));
    }

    /// <inheritdoc />
    public async Task<Result<TransactionDetails>> InquireAsync(string invoiceNo, CancellationToken ct = default)
    {
        var transaction = await transactions.GetByInvoiceNoAsync(invoiceNo, ct).ConfigureAwait(false);
        if (transaction is null)
        {
            return NotFound(invoiceNo);
        }

        if (transaction.Status == TransactionStatus.Pending
            && transaction.PaymentToken is not null
            && await TakeThrottleAsync(invoiceNo, ct).ConfigureAwait(false))
        {
            try
            {
                var result = await gateway.GetPaymentStatusAsync(invoiceNo, ct).ConfigureAwait(false);
                await confirmation.ApplyAsync(result, ct).ConfigureAwait(false);
            }
            catch (PaymentGatewayException exception)
            {
                // The page keeps polling; the next status check, or the notification, settles it.
                LogInquiryFailed(invoiceNo, exception);
            }
        }

        return await GetAsync(invoiceNo, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// At most one inquiry per invoice per <see cref="CheckoutOptions.InquiryThrottle"/>, across
    /// all instances, however often the result page polls. The key is left to expire, not released.
    /// </summary>
    private async Task<bool> TakeThrottleAsync(string invoiceNo, CancellationToken ct)
    {
        var lease = await distributedLock.TryAcquireAsync(
            $"checkout:inquiry:{invoiceNo}",
            options.Value.InquiryThrottle,
            ct).ConfigureAwait(false);

        // Unavailable (Redis down) still inquires: confirming a payment matters more than throttling.
        return lease.Status != LockStatus.Held;
    }

    private static Result<TransactionDetails> NotFound(string invoiceNo) =>
        Result<TransactionDetails>.Failure($"Transaction {invoiceNo} does not exist.", ErrorCodes.TransactionNotFound);

    private static TransactionDetails ToDetails(PaymentTransaction t) => new(
        t.InvoiceNo,
        t.Status,
        t.Amount,
        t.Currency,
        t.CustomerId,
        $"{t.Customer!.FirstName} {t.Customer.LastName}",
        t.ProductId,
        t.Product!.Name,
        t.Product.PaymentType,
        t.Flow,
        t.PaymentChannel,
        t.MaskedCardNo,
        t.RespCode,
        t.RespDesc,
        t.CreatedAt,
        t.PaidAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Payment status check for {InvoiceNo} failed; leaving it Pending.")]
    private partial void LogInquiryFailed(string invoiceNo, Exception exception);
}
