using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Checkout.Infrastructure.Persistence;

/// <summary>Builds an <see cref="AppDbContext"/> for <c>dotnet ef</c>, which runs without the web host.</summary>
/// <remarks>
/// Reads <c>ConnectionStrings__Default</c> from the environment and falls back to the MySQL
/// started by <c>docker compose up mysql</c>.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalConnectionString =
        "Server=127.0.0.1;Port=3306;Database=checkout;User=checkout;Password=checkout";

    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseCheckoutMySql(connectionString);

        return new AppDbContext(options.Options);
    }
}
