using Microsoft.EntityFrameworkCore;

namespace Checkout.Infrastructure.Persistence;

/// <summary>The provider options shared by the running app and <c>dotnet ef</c>.</summary>
internal static class MySqlSetup
{
    /// <summary>
    /// The server version the SQL is generated for. Fixed rather than auto-detected, so building
    /// the model never opens a connection.
    /// </summary>
    public static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

    /// <summary>Configures MySQL and the startup seed.</summary>
    /// <param name="options">The options being built.</param>
    /// <param name="connectionString">The MySQL connection string.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder UseCheckoutMySql(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseMySql(connectionString, ServerVersion)
            // EF Core runs these after applying migrations, at startup and in `dotnet ef database update`.
            .UseSeeding((context, _) => SeedData.Seed(context))
            .UseAsyncSeeding((context, _, cancellationToken) => SeedData.SeedAsync(context, cancellationToken));
}
