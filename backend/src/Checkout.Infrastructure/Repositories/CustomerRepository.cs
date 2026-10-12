using Checkout.Application.Abstractions.Persistence;
using Checkout.Domain.Entities;
using Checkout.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Repositories;

/// <summary><see cref="ICustomerRepository"/> over EF Core.</summary>
public sealed class CustomerRepository(AppDbContext context) : ICustomerRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default) =>
        await context.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        context.Customers.AnyAsync(c => c.Id == id, ct);
}
