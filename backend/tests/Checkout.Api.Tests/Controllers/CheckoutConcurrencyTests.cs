using System.Net;
using Checkout.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Api.Tests.Controllers;

/// <summary>
/// The proof the brief asks for: many parallel checkouts with one idempotency key create exactly
/// one transaction and call 2C2P exactly once — against real MySQL and Redis.
/// </summary>
public sealed class CheckoutConcurrencyTests
{
    private const int ParallelRequests = 10;

    private static async Task<CheckoutClient.Response[]> FireInParallelAsync(ApiFactory factory, string key, object body)
    {
        using var http = factory.CreateClient();

        // Release every request at the same moment.
        var start = new TaskCompletionSource();
        var requests = Enumerable.Range(0, ParallelRequests)
            .Select(async _ =>
            {
                await start.Task;
                return await CheckoutClient.PostAsync(http, body, key);
            })
            .ToList();

        var started = DateTime.UtcNow;
        start.SetResult();
        var responses = await Task.WhenAll(requests);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{ParallelRequests} requests in {(DateTime.UtcNow - started).TotalMilliseconds:F0} ms: " +
            $"{responses.Count(r => r.Status == HttpStatusCode.OK && !r.Replayed)} created, " +
            $"{responses.Count(r => r.Replayed)} replayed, " +
            $"{responses.Count(r => r.Status == HttpStatusCode.Conflict)} conflict");

        return responses;
    }

    [Fact]
    public async Task Parallel_requests_with_one_key_create_one_transaction_and_one_2C2P_call()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.FromMilliseconds(500) });
        var key = Guid.NewGuid().ToString();

        var responses = await FireInParallelAsync(factory, key, new { customerId = 1, productId = 1 });

        var rows = await factory.QueryAsync(db => db.Transactions.CountAsync(t => t.IdempotencyKey == key));
        rows.Should().Be(1, "one purchase must be one transaction");
        factory.Gateway.SessionCalls.Should().Be(1, "2C2P must be called once per purchase");

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.OK || r.Status == HttpStatusCode.Conflict);
        var ok = responses.Where(r => r.Status == HttpStatusCode.OK).ToList();
        ok.Should().NotBeEmpty();
        ok.Select(r => r.Body!.InvoiceNo).Distinct().Should().ContainSingle("every caller gets the same invoice");
        ok.Select(r => r.Body!.RedirectUrl).Distinct().Should().ContainSingle("every caller gets the same hosted page");
        ok.Count(r => !r.Replayed).Should().Be(1, "exactly one request did the work; the rest replayed it");
    }

    [Fact]
    public async Task Without_the_redis_lock_the_unique_index_still_allows_only_one()
    {
        // Redis down: every request skips the lock and races to insert. MySQL decides.
        await using var factory = new ApiFactory(
            new FakePaymentGateway { Delay = TimeSpan.FromMilliseconds(500) },
            new UnavailableLock());
        var key = Guid.NewGuid().ToString();

        var responses = await FireInParallelAsync(factory, key, new { customerId = 2, productId = 3 });

        var rows = await factory.QueryAsync(db => db.Transactions.CountAsync(t => t.IdempotencyKey == key));
        rows.Should().Be(1);
        factory.Gateway.SessionCalls.Should().Be(1);

        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.OK || r.Status == HttpStatusCode.Conflict);
        responses.Count(r => r.Status == HttpStatusCode.OK && !r.Replayed).Should().Be(1);
    }

    [Fact]
    public async Task Different_keys_are_different_purchases()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.FromMilliseconds(100) });
        using var http = factory.CreateClient();

        var first = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 2 }, Guid.NewGuid().ToString());
        var second = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 2 }, Guid.NewGuid().ToString());

        first.Body!.InvoiceNo.Should().NotBe(second.Body!.InvoiceNo);
        factory.Gateway.SessionCalls.Should().Be(2);
    }
}
