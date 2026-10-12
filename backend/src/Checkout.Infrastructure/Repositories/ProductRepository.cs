using Checkout.Application.Abstractions.Persistence;
using Checkout.Domain.Entities;
using Checkout.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Repositories;

/// <summary><see cref="IProductRepository"/> over EF Core.</summary>
public sealed class ProductRepository(AppDbContext context) : IProductRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        await context.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
}
