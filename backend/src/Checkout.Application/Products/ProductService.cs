using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Common;
using Checkout.Application.Products.DTOs;
using Checkout.Domain.Entities;

namespace Checkout.Application.Products;

/// <summary>
/// Default <see cref="IProductService"/>. Reads go cache-aside: Redis first, then the repository
/// on a miss, then back into Redis.
/// </summary>
public sealed class ProductService(IProductRepository productRepository, ICacheService cache) : IProductService
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken ct = default)
    {
        // 1. Try Redis.
        var cached = await cache.GetAsync<IReadOnlyList<ProductDto>>(CacheKeys.Products).ConfigureAwait(false);
        if (cached is not null)
        {
            return Result<IReadOnlyList<ProductDto>>.Success(cached);
        }

        // 2. Miss: load from MySQL through the repository.
        var products = await productRepository.GetAllAsync(ct).ConfigureAwait(false);
        IReadOnlyList<ProductDto> dtos = products.Select(ToDto).ToList();

        // 3. Save to Redis for next time.
        await cache.SetAsync(CacheKeys.Products, dtos, CacheKeys.CatalogTtl).ConfigureAwait(false);
        return Result<IReadOnlyList<ProductDto>>.Success(dtos);
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.Currency, p.PaymentType);
}
