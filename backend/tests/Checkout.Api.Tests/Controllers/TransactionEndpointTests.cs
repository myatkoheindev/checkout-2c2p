using System.Net;
using System.Net.Http.Json;
using Checkout.Api.Tests.Support;
using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Enums;

namespace Checkout.Api.Tests.Controllers;

public sealed class TransactionEndpointTests
{
    private sealed record Details(
        string InvoiceNo,
        TransactionStatus Status,
        decimal Amount,
        string Currency,
        int CustomerId,
        string CustomerName,
        int ProductId,
        string ProductName,
        string PaymentType,
        string Flow,
        string? PaymentChannel,
        string? MaskedCardNo,
        string? RespCode,
        string? RespDesc,
        DateTime CreatedAt,
        DateTime? PaidAt);

    private static Task<Details?> GetAsync(HttpClient http, string invoiceNo) =>
        http.GetFromJsonAsync<Details>($"/api/transactions/{invoiceNo}", CheckoutClient.Json);

    private static async Task<Details?> InquireAsync(HttpClient http, string invoiceNo)
    {
        var response = await http.PostAsync($"/api/transactions/{invoiceNo}/inquiry", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<Details>(CheckoutClient.Json);
    }

    [Fact]
    public async Task Returns_what_the_result_page_shows()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 1, productId: 3);

        var details = await GetAsync(http, invoiceNo);

        details!.Status.Should().Be(TransactionStatus.Pending);
        details.Amount.Should().Be(199.00m);
        details.Currency.Should().Be("SGD");
        details.CustomerId.Should().Be(1);
        details.CustomerName.Should().Be("Wei Ling Tan");
        details.ProductId.Should().Be(3);
        details.ProductName.Should().Be("Monthly Unlimited Membership");
        details.PaymentType.Should().Be("Recurring");
        details.Flow.Should().Be("HostedPage");
    }

    [Fact]
    public async Task Reflects_a_notification_immediately_because_status_is_never_cached()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 2, productId: 1);

        (await GetAsync(http, invoiceNo))!.Status.Should().Be(TransactionStatus.Pending);
        await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 35.00m)));

        var after = await GetAsync(http, invoiceNo);
        after!.Status.Should().Be(TransactionStatus.Succeeded);
        after.MaskedCardNo.Should().Be("411111XXXXXX1111");
        after.PaymentChannel.Should().Be("CC");
        after.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task An_unknown_invoice_is_404()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway());
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/api/transactions/INV000000000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Inquiry_settles_a_pending_payment_and_is_throttled()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Pending, 0m, "", "2002", "Transaction not found.", null, null, null, null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 3, productId: 1);

        // The customer is still on the hosted page: 2002 means no outcome yet, not a failure.
        (await InquireAsync(http, invoiceNo))!.Status.Should().Be(TransactionStatus.Pending);

        // Polled again at once: answered from the database, 2C2P not asked again.
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Succeeded, 35.00m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "T1", null);
        (await InquireAsync(http, invoiceNo))!.Status.Should().Be(TransactionStatus.Pending);
        gateway.StatusChecks.Should().ContainSingle();
    }

    [Fact]
    public async Task Inquiry_confirms_a_saved_card_payment_after_3DS()
    {
        var gateway = new FakePaymentGateway
        {
            Delay = TimeSpan.Zero,
            OnChargeSavedCard = _ => new SavedCardCharge(ChargeOutcome.RedirectRequired, "https://demo2.2c2p.com/3ds", "1001", "Redirect to authenticate ACS bank page."),
        };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Succeeded, 90.00m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "T42", null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var cardId = await factory.QueryAsync(async db =>
        {
            var card = new Domain.Entities.CustomerCard(1, $"tok{Guid.NewGuid():N}"[..20], "411111XXXXXX1111", DateTime.UtcNow);
            db.CustomerCards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
        });

        var checkout = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 2, customerCardId = cardId }, Guid.NewGuid().ToString());
        checkout.Body!.RedirectUrl.Should().Be("https://demo2.2c2p.com/3ds");

        // The browser comes back from 3DS; the result page asks for confirmation.
        var details = await InquireAsync(http, checkout.Body.InvoiceNo);

        details!.Status.Should().Be(TransactionStatus.Succeeded);
        details.Flow.Should().Be("SavedCard");
        details.MaskedCardNo.Should().Be("411111XXXXXX1111");
    }

    [Fact]
    public async Task Inquiry_on_a_final_transaction_does_not_call_2C2P()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 1, productId: 2);
        await Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 90.00m)));

        (await InquireAsync(http, invoiceNo))!.Status.Should().Be(TransactionStatus.Succeeded);
        gateway.StatusChecks.Should().BeEmpty();
    }

    [Fact]
    public async Task A_notification_racing_an_inquiry_settles_once()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Succeeded, 35.00m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "INQ", null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var invoiceNo = await Checkouts.StartHostedAsync(http, customerId: 2, productId: 1);

        await Task.WhenAll(
            Notifications.PostAsync(http, Notifications.Sign(Notifications.Success(invoiceNo, 35.00m))),
            http.PostAsync($"/api/transactions/{invoiceNo}/inquiry", content: null));

        var details = await GetAsync(http, invoiceNo);
        details!.Status.Should().Be(TransactionStatus.Succeeded);
        details.RespCode.Should().Be("0000");
    }
}
