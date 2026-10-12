using Checkout.Application.Abstractions.Persistence;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Checkout.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Repositories;

/// <summary><see cref="ITransactionRepository"/> over EF Core.</summary>
public sealed class TransactionRepository(AppDbContext context) : ITransactionRepository
{
    /// <inheritdoc />
    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    /// <inheritdoc />
    public Task<PaymentTransaction?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct = default) =>
        context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.InvoiceNo == invoiceNo, ct);

    /// <inheritdoc />
    public Task<PaymentTransaction?> GetWithDetailsAsync(string invoiceNo, CancellationToken ct = default) =>
        context.Transactions
            .AsNoTracking()
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.InvoiceNo == invoiceNo, ct);

    /// <inheritdoc />
    public async Task<TransactionInsertResult> TryAddAsync(PaymentTransaction transaction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        context.Transactions.Add(transaction);

        try
        {
            // Saved now rather than through IUnitOfWork: the idempotency-key index must reject a
            // second insert right here, so the clash can be reported instead of thrown.
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            return TransactionInsertResult.Added;
        }
        catch (DbUpdateException)
        {
            context.Entry(transaction).State = EntityState.Detached;

            if (await context.Transactions.AnyAsync(t => t.IdempotencyKey == transaction.IdempotencyKey, ct).ConfigureAwait(false))
            {
                return TransactionInsertResult.DuplicateIdempotencyKey;
            }

            if (await context.Transactions.AnyAsync(t => t.InvoiceNo == transaction.InvoiceNo, ct).ConfigureAwait(false))
            {
                return TransactionInsertResult.DuplicateInvoiceNo;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkSucceededIfPendingAsync(long id, PaymentOutcome outcome, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var changed = await context.Transactions
            .Where(t => t.Id == id && t.Status == TransactionStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(t => t.Status, TransactionStatus.Succeeded)
                    .SetProperty(t => t.PaymentChannel, outcome.PaymentChannel)
                    .SetProperty(t => t.MaskedCardNo, outcome.MaskedCardNo)
                    .SetProperty(t => t.ProviderRef, outcome.ProviderRef)
                    .SetProperty(t => t.RespCode, outcome.RespCode)
                    .SetProperty(t => t.RespDesc, outcome.RespDesc)
                    .SetProperty(t => t.PaidAt, outcome.At)
                    .SetProperty(t => t.UpdatedAt, outcome.At),
                ct)
            .ConfigureAwait(false);

        return changed > 0;
    }

    /// <inheritdoc />
    public async Task<bool> MarkFailedIfPendingAsync(string invoiceNo, PaymentOutcome outcome, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var changed = await context.Transactions
            .Where(t => t.InvoiceNo == invoiceNo && t.Status == TransactionStatus.Pending)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(t => t.Status, TransactionStatus.Failed)
                    .SetProperty(t => t.PaymentChannel, t => outcome.PaymentChannel ?? t.PaymentChannel)
                    .SetProperty(t => t.MaskedCardNo, t => outcome.MaskedCardNo ?? t.MaskedCardNo)
                    .SetProperty(t => t.ProviderRef, t => outcome.ProviderRef ?? t.ProviderRef)
                    .SetProperty(t => t.RespCode, outcome.RespCode)
                    .SetProperty(t => t.RespDesc, outcome.RespDesc)
                    .SetProperty(t => t.UpdatedAt, outcome.At),
                ct)
            .ConfigureAwait(false);

        return changed > 0;
    }
}
