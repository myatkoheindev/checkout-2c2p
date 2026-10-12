using Checkout.Domain.Entities;

namespace Checkout.Application.Abstractions.Persistence;

/// <summary>Reads the seeded customers.</summary>
public interface ICustomerRepository
{
    /// <summary>All customers, ordered by id. Not tracked.</summary>
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Whether a customer exists.</summary>
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}
