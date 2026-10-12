using Checkout.Application.Common;
using Checkout.Application.Products.DTOs;

namespace Checkout.Application.Products;

/// <summary>The checkout page's product dropdown.</summary>
public interface IProductService
{
    /// <summary>Every product, ordered by id. Cached.</summary>
    Task<Result<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken ct = default);
}
