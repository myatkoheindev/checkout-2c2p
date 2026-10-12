using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Enums;
using Checkout.Infrastructure.External.TwoC2P;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Checkout.Infrastructure.Tests.External.TwoC2P;

/// <summary>
/// Calls the real 2C2P sandbox, to prove the documented request shapes match what it accepts.
/// Skipped unless <c>TWOC2P_SECRET_KEY</c> is set, so the normal suite never needs the network:
/// <code>set -a; source ../.env; set +a; dotnet test --filter Category=Sandbox</code>
/// </summary>
[Trait("Category", "Sandbox")]
public sealed class TwoC2PSandboxTests
{
    private static TwoC2PGateway? CreateGateway()
    {
        var secret = Environment.GetEnvironmentVariable("TWOC2P_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var options = new TwoC2POptions
        {
            MerchantId = Environment.GetEnvironmentVariable("TWOC2P_MERCHANT_ID") ?? "JT01",
            SecretKey = secret,
        };
        var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl + "/"), Timeout = options.Timeout };

        return new TwoC2PGateway(http, Options.Create(options), NullLogger<TwoC2PGateway>.Instance);
    }

    private static string NewInvoice() =>
        $"INVTEST{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 1000)}";

    private static PaymentSessionRequest Request(PaymentType type, string invoiceNo) =>
        PaymentSessionRequest.ForCheckout(
            type,
            invoiceNo,
            "Sandbox smoke test",
            35.00m,
            "SGD",
            $"http://localhost:3000/payment/return?invoiceNo={invoiceNo}",
            "https://example.com/api/payments/2c2p/notification",
            savedCardToken: null);

    [Theory]
    [InlineData(PaymentType.OneTime)]
    [InlineData(PaymentType.Recurring)]
    public async Task Sandbox_issues_a_payment_token_and_hosted_page(PaymentType type)
    {
        var gateway = CreateGateway();
        if (gateway is null)
        {
            Assert.Skip("TWOC2P_SECRET_KEY is not set.");
            return;
        }

        var invoiceNo = NewInvoice();
        var result = await gateway.CreateSessionAsync(Request(type, invoiceNo), TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine($"{type} {invoiceNo}: {result.ResponseCode} {result.ResponseMessage}");
        TestContext.Current.TestOutputHelper?.WriteLine($"hosted page: {result.RedirectUrl}");

        result.IsCreated.Should().BeTrue(result.ResponseMessage);
        result.SessionId.Should().NotBeNullOrWhiteSpace();
        result.RedirectUrl.Should().StartWith("https://");
    }

    [Fact]
    public async Task Sandbox_answers_an_inquiry_for_an_unpaid_invoice()
    {
        var gateway = CreateGateway();
        if (gateway is null)
        {
            Assert.Skip("TWOC2P_SECRET_KEY is not set.");
            return;
        }

        var invoiceNo = NewInvoice();
        var session = await gateway.CreateSessionAsync(Request(PaymentType.OneTime, invoiceNo), TestContext.Current.CancellationToken);
        session.IsCreated.Should().BeTrue(session.ResponseMessage);

        var result = await gateway.GetPaymentStatusAsync(invoiceNo, TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine($"inquiry {invoiceNo}: {result.ResponseCode} {result.ResponseMessage}");

        result.InvoiceNo.Should().Be(invoiceNo);
        result.Status.Should().Be(PaymentStatus.Pending, "an unpaid invoice has no outcome yet and must not be failed");
    }
}
