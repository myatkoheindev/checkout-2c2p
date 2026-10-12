using Checkout.Domain.Entities;

namespace Checkout.Application.Abstractions.Persistence;

/// <summary>Cards saved through 2C2P tokenization.</summary>
public interface ICustomerCardRepository
{
    /// <summary>A customer's cards, newest first. Not tracked.</summary>
    Task<IReadOnlyList<CustomerCard>> GetByCustomerAsync(int customerId, CancellationToken ct = default);

    /// <summary>A card, only if it belongs to <paramref name="customerId"/>; otherwise <c>null</c>.</summary>
    Task<CustomerCard?> GetForCustomerAsync(int cardId, int customerId, CancellationToken ct = default);

    /// <summary>
    /// Saves a card unless the customer already has this token. Safe under concurrency: the unique
    /// index on (customer_id, customer_token) decides, and a lost race is not an error.
    /// </summary>
    /// <returns><c>true</c> if this call saved it.</returns>
    Task<bool> AddIfMissingAsync(CustomerCard card, CancellationToken ct = default);
}
