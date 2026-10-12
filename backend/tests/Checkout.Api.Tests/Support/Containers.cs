using Testcontainers.MySql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Checkout.Api.Tests.Support.Containers))]

namespace Checkout.Api.Tests.Support;

/// <summary>
/// One MySQL 8 and one Redis for the whole test run: an xUnit assembly fixture, started before the
/// first test and disposed after the last.
/// </summary>
public sealed class Containers : IAsyncLifetime
{
    public static MySqlContainer MySql { get; } = new MySqlBuilder("mysql:8.0")
        .WithDatabase("checkout")
        .WithUsername("checkout")
        .WithPassword("checkout")
        .Build();

    public static RedisContainer Redis { get; } = new RedisBuilder("redis:7-alpine").Build();

    public async ValueTask InitializeAsync() => await Task.WhenAll(MySql.StartAsync(), Redis.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await MySql.DisposeAsync();
        await Redis.DisposeAsync();
    }
}
