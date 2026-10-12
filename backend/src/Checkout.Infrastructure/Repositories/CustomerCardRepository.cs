using Checkout.Application.Abstractions.Persistence;
using Checkout.Domain.Entities;
using Checkout.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Repositories;

/// <summary><see cref="ICustomerCardRepository"/> over EF Core.</summary>
public sealed class CustomerCardRepository(AppDbContext context) : ICustomerCardRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerCard>> GetByCustomerAsync(int customerId, CancellationToken ct = default) =>
        await context.CustomerCards
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<CustomerCard?> GetForCustomerAsync(int cardId, int customerId, CancellationToken ct = default) =>
        context.CustomerCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId && c.CustomerId == customerId, ct);

    /// <inheritdoc />
    public async Task<bool> AddIfMissingAsync(CustomerCard card, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (await ExistsAsync(card, ct).ConfigureAwait(false))
        {
            return false;
        }

        context.CustomerCards.Add(card);

        try
        {
            // Saved now rather than through IUnitOfWork: the (customer, token) unique index must
            // reject a duplicate card right here, so a lost race is not an error.
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            context.Entry(card).State = EntityState.Detached;

            // A concurrent notification saved it first: the unique index did its job.
            if (await ExistsAsync(card, ct).ConfigureAwait(false))
            {
                return false;
            }

            throw;
        }
    }

    private Task<bool> ExistsAsync(CustomerCard card, CancellationToken ct) =>
        context.CustomerCards.AsNoTracking().AnyAsync(
            c => c.CustomerId == card.CustomerId && c.CustomerToken == card.CustomerToken,
            ct);
}
