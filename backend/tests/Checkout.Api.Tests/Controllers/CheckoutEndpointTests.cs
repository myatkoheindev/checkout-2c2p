using System.Net;
using Checkout.Api.Tests.Support;
using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Entities;
using Checkout.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Checkout.Api.Tests.Controllers;

public sealed class CheckoutEndpointTests
{
    private static async Task<int> SaveCardAsync(ApiFactory factory, int customerId, string token)
    {
        return await factory.QueryAsync(async db =>
        {
            var card = new CustomerCard(customerId, token, "411111XXXXXX1111", DateTime.UtcNow);
            db.CustomerCards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
        });
    }

    [Fact]
    public async Task OneTime_returns_the_hosted_page_without_channel_restriction()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1 }, Guid.NewGuid().ToString());

        response.Status.Should().Be(HttpStatusCode.OK, response.Raw);
        response.Body!.Status.Should().Be(TransactionStatus.Pending);
        response.Body.InvoiceNo.Should().MatchRegex("^INV[0-9]{18}$");
        response.Body.RedirectUrl.Should().StartWith("https://sandbox-pgw-ui.2c2p.com/");

        var sent = factory.Gateway.SessionRequests.Single();
        sent.CardOnly.Should().BeFalse();
        sent.Amount.Should().Be(35.00m);
        sent.Currency.Should().Be("SGD");
        sent.InvoiceNo.Should().Be(response.Body.InvoiceNo);
        sent.ReturnUrl.Should().Be($"http://localhost:3000/payment/return?invoiceNo={response.Body.InvoiceNo}");
        sent.NotificationUrl.Should().Be("https://checkout-test.example.com/api/payments/2c2p/notification");

        var stored = await factory.QueryAsync(db => db.Transactions.SingleAsync(t => t.InvoiceNo == response.Body.InvoiceNo));
        stored.Status.Should().Be(TransactionStatus.Pending);
        stored.Flow.Should().Be(PaymentFlow.HostedPage);
        stored.PaymentToken.Should().Be($"pt_{response.Body.InvoiceNo}");
        stored.RedirectUrl.Should().Be(response.Body.RedirectUrl);
    }

    [Fact]
    public async Task Recurring_asks_for_card_only_and_offers_to_save_the_card()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();

        await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 3 }, Guid.NewGuid().ToString());

        var sent = factory.Gateway.SessionRequests.Single();
        sent.CardOnly.Should().BeTrue();
        sent.OfferToSaveCard.Should().BeTrue();
        sent.Amount.Should().Be(199.00m);
    }

    [Fact]
    public async Task A_repeated_key_replays_the_original_response_without_calling_2C2P()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        var first = await CheckoutClient.PostAsync(http, new { customerId = 3, productId = 2 }, key);
        var again = await CheckoutClient.PostAsync(http, new { customerId = 3, productId = 2 }, key);

        again.Status.Should().Be(HttpStatusCode.OK);
        again.Replayed.Should().BeTrue();
        first.Replayed.Should().BeFalse();
        again.Body.Should().BeEquivalentTo(first.Body);
        factory.Gateway.SessionCalls.Should().Be(1);
    }

    [Fact]
    public async Task A_repeated_key_with_a_different_body_is_rejected()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero });
        using var http = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1 }, key);
        var other = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 2 }, key);

        other.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        other.Raw.Should().Contain("already used for a different checkout");
        factory.Gateway.SessionCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    public async Task A_missing_or_invalid_key_is_400(string? key)
    {
        await using var factory = new ApiFactory(new FakePaymentGateway());
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1 }, key);

        response.Status.Should().Be(HttpStatusCode.BadRequest);
        response.Raw.Should().Contain("Idempotency-Key");
        factory.Gateway.SessionCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_malformed_body_is_400_not_500()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway());
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId = "one", productId = 1 }, Guid.NewGuid().ToString());

        response.Status.Should().Be(HttpStatusCode.BadRequest, response.Raw);
        factory.Gateway.SessionCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(99, 1, "customerId")]
    [InlineData(1, 99, "productId")]
    public async Task An_unknown_customer_or_product_is_422(int customerId, int productId, string field)
    {
        await using var factory = new ApiFactory(new FakePaymentGateway());
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId, productId }, Guid.NewGuid().ToString());

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Raw.Should().Contain(field).And.Contain("errors");
        factory.Gateway.SessionCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_card_of_another_customer_is_422()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway());
        using var http = factory.CreateClient();
        var othersCard = await SaveCardAsync(factory, customerId: 2, token: $"tok_{Guid.NewGuid():N}"[..20]);

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1, customerCardId = othersCard }, Guid.NewGuid().ToString());

        response.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Raw.Should().Contain("customerCardId");
        factory.Gateway.SessionCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_declined_token_request_fails_the_transaction()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway
        {
            Delay = TimeSpan.Zero,
            OnCreateSession = _ => new PaymentSession(false, null, null, "9015", "Existing invoice number."),
        });
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1 }, Guid.NewGuid().ToString());

        response.Body!.Status.Should().Be(TransactionStatus.Failed);
        response.Body.RedirectUrl.Should().BeNull();
        var stored = await factory.QueryAsync(db => db.Transactions.SingleAsync(t => t.InvoiceNo == response.Body.InvoiceNo));
        stored.RespCode.Should().Be("9015");
        stored.RespDesc.Should().Be("Existing invoice number.");
    }

    [Fact]
    public async Task An_unreachable_2C2P_fails_the_transaction_cleanly()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway { Delay = TimeSpan.Zero, FailCreateSession = true });
        using var http = factory.CreateClient();

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 1 }, Guid.NewGuid().ToString());

        response.Status.Should().Be(HttpStatusCode.OK);
        response.Body!.Status.Should().Be(TransactionStatus.Failed);
    }

    [Fact]
    public async Task A_saved_card_needing_3DS_returns_the_3DS_page()
    {
        await using var factory = new ApiFactory(new FakePaymentGateway
        {
            Delay = TimeSpan.Zero,
            OnChargeSavedCard = _ => new SavedCardCharge(ChargeOutcome.RedirectRequired, "https://demo2.2c2p.com/3ds/abc", "1001", "Redirect to authenticate ACS bank page."),
        });
        using var http = factory.CreateClient();
        var token = $"tok_{Guid.NewGuid():N}"[..20];
        var card = await SaveCardAsync(factory, customerId: 1, token);

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 2, customerCardId = card }, Guid.NewGuid().ToString());

        response.Body!.Status.Should().Be(TransactionStatus.Pending);
        response.Body.RedirectUrl.Should().Be("https://demo2.2c2p.com/3ds/abc");

        var sent = factory.Gateway.SessionRequests.Single();
        sent.SavedCardToken.Should().Be(token);
        sent.CardOnly.Should().BeTrue();
        factory.Gateway.ChargeRequests.Single().SavedCardToken.Should().Be(token);
        factory.Gateway.StatusChecks.Should().BeEmpty("3DS has not happened yet");
    }

    [Fact]
    public async Task A_saved_card_without_3DS_is_confirmed_by_a_status_check()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Succeeded, 90.00m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "T987", null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var card = await SaveCardAsync(factory, customerId: 3, $"tok_{Guid.NewGuid():N}"[..20]);

        var response = await CheckoutClient.PostAsync(http, new { customerId = 3, productId = 2, customerCardId = card }, Guid.NewGuid().ToString());

        response.Body!.Status.Should().Be(TransactionStatus.Succeeded);
        response.Body.RedirectUrl.Should().BeNull("no redirect: go straight to the result page");
        gateway.StatusChecks.Should().ContainSingle().Which.Should().Be(response.Body.InvoiceNo);

        var stored = await factory.QueryAsync(db => db.Transactions.SingleAsync(t => t.InvoiceNo == response.Body.InvoiceNo));
        stored.Flow.Should().Be(PaymentFlow.SavedCard);
        stored.MaskedCardNo.Should().Be("411111XXXXXX1111");
        stored.ProviderRef.Should().Be("T987");
        stored.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_saved_card_whose_status_check_declines_is_failed()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Failed, 0m, "SGD", "4051", "Insufficient funds.", "CC", null, null, null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var card = await SaveCardAsync(factory, customerId: 2, $"tok_{Guid.NewGuid():N}"[..20]);

        var response = await CheckoutClient.PostAsync(http, new { customerId = 2, productId = 1, customerCardId = card }, Guid.NewGuid().ToString());

        response.Body!.Status.Should().Be(TransactionStatus.Failed);
        var stored = await factory.QueryAsync(db => db.Transactions.SingleAsync(t => t.InvoiceNo == response.Body.InvoiceNo));
        stored.RespCode.Should().Be("4051");
        stored.RespDesc.Should().Be("Insufficient funds.");
    }

    [Fact]
    public async Task A_success_with_the_wrong_amount_is_not_marked_paid()
    {
        var gateway = new FakePaymentGateway { Delay = TimeSpan.Zero };
        gateway.OnStatusCheck = invoiceNo => new PaymentResult(invoiceNo, PaymentStatus.Succeeded, 0.01m, "SGD", "0000", "Success", "CC", "411111XXXXXX1111", "T1", null);
        await using var factory = new ApiFactory(gateway);
        using var http = factory.CreateClient();
        var card = await SaveCardAsync(factory, customerId: 1, $"tok_{Guid.NewGuid():N}"[..20]);

        var response = await CheckoutClient.PostAsync(http, new { customerId = 1, productId = 4, customerCardId = card }, Guid.NewGuid().ToString());

        response.Body!.Status.Should().Be(TransactionStatus.Pending, "1890.00 was stored; 0.01 must never count as paid");
    }
}
