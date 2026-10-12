using Checkout.Application.Common;
using Checkout.Application.Customers.DTOs;

namespace Checkout.Application.Customers;

/// <summary>The checkout page's customer dropdown and saved cards.</summary>
public interface ICustomerService
{
    /// <summary>Every customer, ordered by id. Cached.</summary>
    Task<Result<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken ct = default);

    /// <summary>A customer's saved cards, newest first. Cached per customer.</summary>
    /// <returns><see cref="ErrorCodes.CustomerNotFound"/> when the customer does not exist.</returns>
    Task<Result<IReadOnlyList<SavedCardDto>>> GetSavedCardsAsync(int customerId, CancellationToken ct = default);
}
