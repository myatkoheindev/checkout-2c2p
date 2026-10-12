using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Persistence;

/// <summary>The application's unit of work over MySQL.</summary>
/// <remarks>
/// Each entity's mapping lives in <c>Configurations/</c> as an
/// <see cref="IEntityTypeConfiguration{TEntity}"/>. Every <see cref="DateTime"/> is stored and
/// read back as UTC.
/// </remarks>
/// <param name="options">The options configured in <see cref="DependencyInjection.AddInfrastructure"/>.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    public DbSet<CustomerCard> CustomerCards => Set<CustomerCard>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.UseSnakeCaseColumnNames();
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // MySQL DATETIME has no time zone; every value written is UTC, so mark it UTC on the way out.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
