using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Payments;
using Checkout.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Checkout.Api.Tests.Support;

/// <summary>
/// Hosts the real API over the test containers, with 2C2P replaced by <see cref="FakePaymentGateway"/>.
/// </summary>
/// <param name="gateway">The fake 2C2P this host talks to.</param>
/// <param name="distributedLock">Replaces the Redis lock, to simulate Redis being down.</param>
public sealed class ApiFactory(FakePaymentGateway gateway, IDistributedLock? distributedLock = null) : WebApplicationFactory<Program>
{
    public FakePaymentGateway Gateway { get; } = gateway;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Containers.MySql.GetConnectionString(),
                ["ConnectionStrings:Redis"] = Containers.Redis.GetConnectionString(),
                ["Frontend:Url"] = "http://localhost:3000",
                ["Checkout:PublicApiUrl"] = "https://checkout-test.example.com",
                ["Checkout:LockWait"] = "00:00:15",
                ["TwoC2P:MerchantId"] = "JT01",
                ["TwoC2P:SecretKey"] = "TEST-SECRET-KEY",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Gateway);

            if (distributedLock is not null)
            {
                services.RemoveAll<IDistributedLock>();
                services.AddSingleton(distributedLock);
            }
        });
    }

    /// <summary>Runs a query against the test database.</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
