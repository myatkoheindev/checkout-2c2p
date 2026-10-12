using Checkout.Application.Abstractions.Persistence;

namespace Checkout.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> over the request's <see cref="AppDbContext"/>, the same scoped instance
/// the repositories use, so it commits whatever they staged.
/// </summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
