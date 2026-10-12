using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Infrastructure.Caching;
using Checkout.Infrastructure.External.TwoC2P;
using Checkout.Infrastructure.Persistence;
using Checkout.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Checkout.Infrastructure;

/// <summary>Registers the Infrastructure implementations: MySQL, repositories, unit of work, Redis, 2C2P.</summary>
public static class DependencyInjection
{
    /// <summary>The connection string name read from <c>ConnectionStrings</c>.</summary>
    public const string ConnectionStringName = "Default";

    /// <summary>The Redis connection string name read from <c>ConnectionStrings</c>.</summary>
    public const string RedisConnectionStringName = "Redis";

    /// <summary>
    /// Adds the adapters: EF Core on MySQL with its repositories, Redis (cache and lock), and the
    /// 2C2P HTTP client. No connection is opened here.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration, for the 2C2P section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Connection strings are read when the services are first resolved, not here, so a host
        // that adds configuration later (the integration tests) is honoured.
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseCheckoutMySql(RequiredConnectionString(provider, ConnectionStringName)));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerCardRepository, CustomerCardRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // One multiplexer per process, as StackExchange.Redis intends. AbortOnConnectFail=false
        // lets the API start while Redis is down and reconnect in the background.
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(RequiredConnectionString(provider, RedisConnectionStringName));
            options.AbortOnConnectFail = false;

            // Fail fast: a cache that answers slowly is worse than no cache.
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;
            options.SyncTimeout = 1000;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();

        // Validated on first use, not at startup: the read endpoints work before a key is set.
        services.AddOptions<TwoC2POptions>()
            .Bind(configuration.GetSection(TwoC2POptions.SectionName))
            .ValidateDataAnnotations();

        services.AddHttpClient<IPaymentGateway, TwoC2PGateway>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<TwoC2POptions>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = settings.Timeout;
        });

        return services;
    }

    /// <summary>
    /// Applies pending migrations, then seeds customers and products. Safe to run on every
    /// start: both steps skip what is already there.
    /// </summary>
    /// <param name="services">The root service provider.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync(ct).ConfigureAwait(false);
        }
    }

    private static string RequiredConnectionString(IServiceProvider provider, string name) =>
        provider.GetRequiredService<IConfiguration>().GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured. Set ConnectionStrings__{name}.");
}
