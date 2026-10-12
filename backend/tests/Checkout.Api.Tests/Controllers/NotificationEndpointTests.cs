using System.Net;
using System.Net.Http.Json;
using Checkout.Api.Tests.Support;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Api.Tests.Controllers;

public sealed class NotificationEndpointTests
{
    private static ApiFactory NewFactory() => new(new FakePaymentGateway { Delay = TimeSpan.Zero });

    private static Task<PaymentTransaction> LoadAsync(ApiFactory factory, string invoiceNo) =>
        factory.QueryAsync(db => db.Transactions.AsNoTracking().SingleAsync(t => t.InvoiceNo == invoiceNo));

    [Fact]
    public async Task A_successful_notification_marks_the_transaction_succeeded()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 1, productId: 1);

        var response = await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 35.00m)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await LoadAsync(factory, invoiceNo);
        stored.Status.Should().Be(TransactionStatus.Succeeded);
        stored.PaymentChannel.Should().Be("CC");
        stored.MaskedCardNo.Should().Be("411111XXXXXX1111");
        stored.ProviderRef.Should().Be("2868821");
        stored.RespCode.Should().Be("0000");
        stored.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_declined_notification_marks_it_failed_with_the_reason()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 2, productId: 2);

        await Notifications.PostAsync(http, Notifications.Sign(Notifications.Declined(invoiceNo, 90.00m)));

        var stored = await LoadAsync(factory, invoiceNo);
        stored.Status.Should().Be(TransactionStatus.Failed);
        stored.RespCode.Should().Be("4051");
        stored.RespDesc.Should().Be("Insufficient funds.");
        stored.PaidAt.Should().BeNull();
    }

    [Fact]
    public async Task A_notification_with_a_bad_signature_is_rejected_and_changes_nothing()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 1, productId: 2);

        var response = await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 90.00m), secretKey: "attacker-key"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LoadAsync(factory, invoiceNo)).Status.Should().Be(TransactionStatus.Pending);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"payload":""}""")]
    [InlineData("""{"payload":"not.a.jwt"}""")]
    public async Task A_malformed_notification_is_rejected(string body)
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();

        var response = await http.PostAsync(
            "/api/payments/2c2p/notification",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_success_for_the_wrong_amount_is_not_applied()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 3, productId: 4);

        var response = await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 1.00m)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "acknowledged, so 2C2P stops retrying");
        (await LoadAsync(factory, invoiceNo)).Status.Should().Be(TransactionStatus.Pending, "1890.00 was stored");
    }

    [Fact]
    public async Task A_late_decline_cannot_undo_a_success()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 1, productId: 1);

        await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 35.00m)));
        var late = await Notifications.PostAsync(http, Notifications.Sign(Notifications.Declined(invoiceNo, 35.00m)));

        late.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await LoadAsync(factory, invoiceNo);
        stored.Status.Should().Be(TransactionStatus.Succeeded);
        stored.RespCode.Should().Be("0000");
    }

    [Fact]
    public async Task An_unknown_invoice_is_acknowledged_and_ignored()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();

        var response = await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success("INV000000000000000000", 35.00m)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_recurring_payment_saves_the_card_once_however_often_2C2P_repeats_it()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 2, productId: 3);
        var token = $"tok{Guid.NewGuid():N}"[..20];
        var notification = Notifications.Sign(Notifications.Success(invoiceNo, 199.00m, customerToken: token, accountNo: "520000XXXXXX0007"));

        // The same notification three times in a row, then ten at once.
        for (var i = 0; i < 3; i++)
        {
            (await Notifications.PostAsync(http, notification)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var parallel = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Notifications.PostAsync(http, notification)));
        parallel.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var cards = await factory.QueryAsync(db => db.CustomerCards.Where(c => c.CustomerToken == token).ToListAsync());
        cards.Should().ContainSingle();
        cards[0].CustomerId.Should().Be(2);
        cards[0].MaskedCardNo.Should().Be("520000XXXXXX0007");

        var stored = await LoadAsync(factory, invoiceNo);
        stored.Status.Should().Be(TransactionStatus.Succeeded);
    }

    [Fact]
    public async Task A_saved_card_appears_in_the_customer_list_at_once()
    {
        await using var factory = NewFactory();
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 3, productId: 3);
        var token = $"tok{Guid.NewGuid():N}"[..20];

        // Fill the cache first, so the test proves invalidation rather than a cold read.
        await http.GetFromJsonAsync<List<SavedCard>>("/api/customers/3/cards");

        await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 199.00m, customerToken: token, accountNo: "411111XXXXXX9999")));

        var cards = await http.GetFromJsonAsync<List<SavedCard>>("/api/customers/3/cards");
        cards.Should().Contain(c => c.MaskedCardNo == "411111XXXXXX9999");
    }

    private sealed record SavedCard(int Id, string MaskedCardNo);
}
