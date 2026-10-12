using Checkout.Domain.Entities;

namespace Checkout.Application.Abstractions.Persistence;

/// <summary>Payment transactions.</summary>
public interface ITransactionRepository
{
    /// <summary>The transaction for an idempotency key, or <c>null</c>. Not tracked.</summary>
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>The transaction for an invoice number, or <c>null</c>. Not tracked.</summary>
    Task<PaymentTransaction?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default);

    /// <summary>The transaction with its customer and product loaded, or <c>null</c>. Not tracked.</summary>
    Task<PaymentTransaction?> GetWithDetailsAsync(string invoiceNo, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new transaction and saves it at once, then keeps tracking it so later changes are
    /// written by <see cref="IUnitOfWork.SaveChangesAsync"/>. It saves immediately, unlike other
    /// staged changes: a clash on a unique index must be caught here and reported, not thrown,
    /// because the idempotency-key index is the last guard against a double charge.
    /// </summary>
    Task<TransactionInsertResult> TryAddAsync(PaymentTransaction transaction, CancellationToken ct = default);

    /// <summary>
    /// Moves a transaction from Pending to Succeeded in one conditional update
    /// (<c>WHERE status = 'Pending'</c>), so a final status is never overwritten.
    /// </summary>
    /// <returns><c>true</c> if this call changed it.</returns>
    Task<bool> MarkSucceededIfPendingAsync(long id, PaymentOutcome outcome, CancellationToken ct = default);

    /// <summary>Moves a transaction from Pending to Failed in one conditional update.</summary>
    /// <returns><c>true</c> if this call changed it.</returns>
    Task<bool> MarkFailedIfPendingAsync(string invoiceNo, PaymentOutcome outcome, CancellationToken ct = default);
}

/// <summary>What happened to an insert.</summary>
public enum TransactionInsertResult
{
    Added,

    /// <summary>Another transaction already holds this idempotency key.</summary>
    DuplicateIdempotencyKey,

    /// <summary>The invoice number is taken; generate another and retry.</summary>
    DuplicateInvoiceNo,
}

/// <summary>The provider's answer recorded on a transaction when it becomes final.</summary>
/// <param name="RespCode">2C2P's response code.</param>
/// <param name="RespDesc">2C2P's description.</param>
/// <param name="PaymentChannel">How it was paid, e.g. <c>VI</c>.</param>
/// <param name="MaskedCardNo">The masked card or account number.</param>
/// <param name="ProviderRef">2C2P's transaction reference.</param>
/// <param name="At">When, in UTC.</param>
public sealed record PaymentOutcome(
    string RespCode,
    string? RespDesc,
    string? PaymentChannel,
    string? MaskedCardNo,
    string? ProviderRef,
    DateTime At);
