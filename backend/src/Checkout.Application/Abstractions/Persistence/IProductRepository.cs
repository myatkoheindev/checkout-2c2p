using Checkout.Domain.Entities;

namespace Checkout.Application.Abstractions.Persistence;

/// <summary>Reads the seeded products.</summary>
public interface IProductRepository
{
    /// <summary>All products, ordered by id. Not tracked.</summary>
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);

    /// <summary>One product, or <c>null</c>. Not tracked.</summary>
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
}
