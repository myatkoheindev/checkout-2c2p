using System.Net;
using System.Text.Json;
using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Enums;
using Checkout.Domain.Exceptions;
using Checkout.Infrastructure.External.TwoC2P;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Checkout.Infrastructure.Tests.External.TwoC2P;

public sealed class TwoC2PGatewayTests
{
    private const string Secret = "TEST-SECRET-KEY";
    private const string Invoice = "INV202610090000000001";

    private static (TwoC2PGateway Gateway, StubHttpHandler Handler) Create(Func<HttpRequestMessage, string, HttpResponseMessage> respond)
    {
        var handler = new StubHttpHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox-pgw.2c2p.com/") };
        var options = Options.Create(new TwoC2POptions { MerchantId = "JT01", SecretKey = Secret });

        return (new TwoC2PGateway(http, options, NullLogger<TwoC2PGateway>.Instance), handler);
    }

    private static HttpResponseMessage SignedPayload(string json, string secret = Secret) =>
        StubHttpHandler.Json(JsonSerializer.Serialize(new { payload = Hs256Jwt.Sign(json, secret) }));

    private static JsonElement DecodeSentPayload(string body)
    {
        using var envelope = JsonDocument.Parse(body);
        Hs256Jwt.TryVerify(envelope.RootElement.GetProperty("payload").GetString(), Secret, out var json)
            .Should().BeTrue("every request must be signed with the merchant secret key");

        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static PaymentSessionRequest SessionRequest(PaymentType type, string? savedCard = null) =>
        PaymentSessionRequest.ForCheckout(type, Invoice, "Yoga Drop-in Class", 35m, "SGD", "http://front/return", "https://api/notify", savedCard);

    private static string ResultJson(string respCode, string merchantId = "JT01", string? tranRef = "T1", string? referenceNo = null) =>
        JsonSerializer.Serialize(new
        {
            merchantID = merchantId,
            invoiceNo = "INV1",
            amount = 35.0m,
            currencyCode = "SGD",
            channelCode = "CC",
            accountNo = "411111XXXXXX1111",
            tranRef,
            referenceNo,
            respCode,
            respDesc = "desc",
        });

    private const string TokenSuccess =
        """{"webPaymentUrl":"https://sandbox-pgw-ui.2c2p.com/payment/4.3/#/token/abc","paymentToken":"abc","respCode":"0000","respDesc":"Success"}""";

    [Fact]
    public async Task Payment_token_is_posted_signed_to_the_v43_endpoint()
    {
        var (gateway, handler) = Create((_, _) => SignedPayload(TokenSuccess));

        var result = await gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime));

        result.IsCreated.Should().BeTrue();
        result.SessionId.Should().Be("abc");
        result.RedirectUrl.Should().StartWith("https://sandbox-pgw-ui.2c2p.com/");

        var (request, body) = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/paymenttoken");

        var sent = DecodeSentPayload(body);
        sent.GetProperty("merchantID").GetString().Should().Be("JT01");
        sent.GetProperty("invoiceNo").GetString().Should().Be(Invoice);
        sent.GetProperty("description").GetString().Should().Be("Yoga Drop-in Class");
        sent.GetProperty("amount").GetDecimal().Should().Be(35.00m);
        sent.GetProperty("currencyCode").GetString().Should().Be("SGD");
        sent.GetProperty("frontendReturnUrl").GetString().Should().Be("http://front/return");
        sent.GetProperty("backendReturnUrl").GetString().Should().Be("https://api/notify");
        sent.TryGetProperty("paymentChannel", out _).Should().BeFalse("OneTime must not restrict channels");
        sent.TryGetProperty("tokenize", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Recurring_token_request_is_card_only_and_tokenizes()
    {
        var (gateway, handler) = Create((_, _) => SignedPayload(TokenSuccess));

        await gateway.CreateSessionAsync(SessionRequest(PaymentType.Recurring));

        var sent = DecodeSentPayload(handler.Requests.Single().Body);
        sent.GetProperty("paymentChannel").EnumerateArray().Select(c => c.GetString()).Should().Equal("CC");
        sent.GetProperty("tokenize").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Saved_card_token_request_names_the_customer_token_only()
    {
        var (gateway, handler) = Create((_, _) => SignedPayload(TokenSuccess));

        var result = await gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime, savedCard: "tok_123"));

        result.RedirectUrl.Should().BeNull("a saved card is charged with Do Payment, not the hosted page");
        var sent = DecodeSentPayload(handler.Requests.Single().Body);
        sent.GetProperty("paymentChannel").EnumerateArray().Select(c => c.GetString()).Should().Equal("CC");
        sent.GetProperty("customerToken").EnumerateArray().Select(c => c.GetString()).Should().Equal("tok_123");
        sent.GetProperty("customerTokenOnly").GetBoolean().Should().BeTrue();
        sent.GetProperty("request3DS").GetString().Should().Be("N");
    }

    [Fact]
    public async Task A_declined_token_request_is_a_result_not_an_exception()
    {
        var (gateway, _) = Create((_, _) => SignedPayload("""{"respCode":"9015","respDesc":"Existing invoice number."}"""));

        var result = await gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime));

        result.IsCreated.Should().BeFalse();
        result.ResponseCode.Should().Be("9015");
        result.ResponseMessage.Should().Be("Existing invoice number.");
    }

    [Fact]
    public async Task An_unsigned_error_body_is_read_as_a_result()
    {
        var (gateway, _) = Create((_, _) => StubHttpHandler.Json("""{"respCode":"9042","respDesc":"Hash mismatch."}"""));

        var result = await gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime));

        result.ResponseCode.Should().Be("9042");
        result.IsCreated.Should().BeFalse();
    }

    [Fact]
    public async Task A_response_signed_with_another_key_is_rejected()
    {
        var (gateway, _) = Create((_, _) => SignedPayload(TokenSuccess, secret: "someone-else"));

        var act = () => gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime));

        await act.Should().ThrowAsync<PaymentGatewayException>().WithMessage("*signature*");
    }

    [Fact]
    public async Task An_http_error_is_a_gateway_exception()
    {
        var (gateway, _) = Create((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var act = () => gateway.CreateSessionAsync(SessionRequest(PaymentType.OneTime));

        await act.Should().ThrowAsync<PaymentGatewayException>().WithMessage("*HTTP 502*");
    }

    [Fact]
    public async Task Do_payment_is_plain_json_with_the_customer_token()
    {
        var (gateway, handler) = Create((_, _) => StubHttpHandler.Json(
            """{"channelCode":"CC","respCode":"1001","respDesc":"Redirect to authenticate ACS bank page.","data":"https://demo2.2c2p.com/3ds"}"""));

        var result = await gateway.ChargeSavedCardAsync(new SavedCardChargeRequest("abc", "tok_123", "http://front/return", "203.0.113.7"));

        result.Outcome.Should().Be(ChargeOutcome.RedirectRequired);
        result.RedirectUrl.Should().Be("https://demo2.2c2p.com/3ds");

        var (request, body) = handler.Requests.Single();
        request.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/payment");

        using var sent = JsonDocument.Parse(body);
        var root = sent.RootElement;
        root.TryGetProperty("payload", out _).Should().BeFalse("Do Payment takes plain JSON, not a JWT");
        root.GetProperty("paymentToken").GetString().Should().Be("abc");
        root.GetProperty("responseReturnUrl").GetString().Should().Be("http://front/return");
        root.GetProperty("clientIP").GetString().Should().Be("203.0.113.7");
        root.GetProperty("payment").GetProperty("code").GetProperty("channelCode").GetString().Should().Be("CC");
        root.GetProperty("payment").GetProperty("data").GetProperty("customerToken").GetString().Should().Be("tok_123");
    }

    [Fact]
    public async Task Do_payment_2000_is_submitted_and_confirmed_by_a_status_check()
    {
        var (gateway, _) = Create((_, _) => StubHttpHandler.Json(
            """{"channelCode":"CC","invoiceNo":"INV1","respCode":"2000","respDesc":"Transaction is completed, please do payment inquiry request for full payment information."}"""));

        var result = await gateway.ChargeSavedCardAsync(new SavedCardChargeRequest("abc", "tok_123", "http://front/return"));

        result.Outcome.Should().Be(ChargeOutcome.Submitted);
        result.RedirectUrl.Should().BeNull();
    }

    [Fact]
    public async Task Any_other_do_payment_code_is_declined()
    {
        var (gateway, _) = Create((_, _) => StubHttpHandler.Json(
            """{"channelCode":"CC","invoiceNo":"INV1","respCode":"4051","respDesc":"Insufficient funds."}"""));

        var result = await gateway.ChargeSavedCardAsync(new SavedCardChargeRequest("abc", "tok_123", "http://front/return"));

        result.Outcome.Should().Be(ChargeOutcome.Declined);
        result.ResponseCode.Should().Be("4051");
        result.ResponseMessage.Should().Be("Insufficient funds.");
    }

    [Fact]
    public async Task Payment_inquiry_is_signed_and_its_result_verified()
    {
        var (gateway, handler) = Create((_, _) => SignedPayload(
            """{"merchantID":"JT01","invoiceNo":"INV1","amount":35.0,"currencyCode":"SGD","channelCode":"CC","accountNo":"411111XXXXXX1111","referenceNo":"2785703","respCode":"0000","respDesc":"Transaction is successful."}"""));

        var result = await gateway.GetPaymentStatusAsync("INV1");

        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.Amount.Should().Be(35.0m);
        result.MaskedCardNo.Should().Be("411111XXXXXX1111");
        result.ProviderReference.Should().Be("2785703", "referenceNo stands in when tranRef is absent");

        var (request, body) = handler.Requests.Single();
        request.RequestUri!.ToString().Should().Be("https://sandbox-pgw.2c2p.com/payment/4.3/paymentinquiry");
        var sent = DecodeSentPayload(body);
        sent.GetProperty("merchantID").GetString().Should().Be("JT01");
        sent.GetProperty("invoiceNo").GetString().Should().Be("INV1");
    }

    [Fact]
    public async Task An_inquiry_with_no_outcome_yet_is_pending_not_failed()
    {
        // Recorded from the sandbox: an unpaid invoice is answered 2002 with invoiceNo null.
        var (gateway, _) = Create((_, _) => SignedPayload(
            """{"accountNo":null,"customerToken":null,"merchantID":"JT01","invoiceNo":null,"amount":0.0,"currencyCode":null,"tranRef":null,"referenceNo":null,"channelCode":null,"respCode":"2002","respDesc":"Transaction not found."}"""));

        var result = await gateway.GetPaymentStatusAsync("INV1");

        result.InvoiceNo.Should().Be("INV1", "the invoice asked about stands in for the missing one");
        result.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task A_success_without_amount_is_rejected()
    {
        var (gateway, _) = Create((_, _) => SignedPayload(
            """{"merchantID":"JT01","invoiceNo":"INV1","currencyCode":"SGD","respCode":"0000","respDesc":"Success"}"""));

        var act = () => gateway.GetPaymentStatusAsync("INV1");

        await act.Should().ThrowAsync<PaymentGatewayException>().WithMessage("*amount*");
    }

    [Fact]
    public void A_valid_notification_is_decoded_including_a_string_amount_and_customer_token()
    {
        var (gateway, _) = Create((_, _) => throw new InvalidOperationException("a notification makes no call"));
        var token = Hs256Jwt.Sign(
            """{"merchantID":"JT01","invoiceNo":"INV1","accountNo":"411111XXXXXX1111","amount":"199.00","currencyCode":"SGD","tranRef":"2868821","channelCode":"CC","customerToken":"tok_new","respCode":"0000","respDesc":"Success"}""",
            Secret);

        var result = gateway.ParseNotification(token);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(199.00m);
        result.ProviderReference.Should().Be("2868821");
        result.SavedCardToken.Should().Be("tok_new");
        result.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task With_multi_currency_pricing_the_charged_amount_is_the_fx_amount()
    {
        // Recorded from a real sandbox payment: JT01 settles in THB, the customer paid SGD 35.00.
        var (gateway, _) = Create((_, _) => SignedPayload(
            """{"accountNo":"411111XXXXXX1111","merchantID":"JT01","invoiceNo":"INV202610093345340429","amount":848.24,"fxAmount":35.0,"fxRate":24.2354868,"fxCurrencyCode":"SGD","currencyCode":"THB","tranRef":"15677813","referenceNo":"11173250","channelCode":"VI","respCode":"0000","respDesc":"Success"}"""));

        var result = await gateway.GetPaymentStatusAsync("INV202610093345340429");

        result.Amount.Should().Be(35.0m);
        result.Currency.Should().Be("SGD");
        result.PaymentMethod.Should().Be("VI");
        result.ProviderReference.Should().Be("15677813", "tranRef wins over referenceNo");
    }

    [Fact]
    public void A_forged_notification_is_rejected()
    {
        var (gateway, _) = Create((_, _) => throw new InvalidOperationException("a notification makes no call"));
        var forged = Hs256Jwt.Sign("""{"invoiceNo":"INV1","amount":35,"currencyCode":"SGD","respCode":"0000"}""", "attacker-key");

        gateway.ParseNotification(forged).Should().BeNull();
    }

    [Theory]
    [InlineData("0000", PaymentStatus.Succeeded)]
    [InlineData("0001", PaymentStatus.Pending)]
    [InlineData("2001", PaymentStatus.Pending)]
    [InlineData("2002", PaymentStatus.Pending)]
    [InlineData("4009", PaymentStatus.Pending)]
    [InlineData("2000", PaymentStatus.Failed)]
    [InlineData("4005", PaymentStatus.Failed)]
    [InlineData("9015", PaymentStatus.Failed)]
    public void Response_codes_map_to_a_neutral_status(string respCode, PaymentStatus expected)
    {
        var (gateway, _) = Create((_, _) => throw new InvalidOperationException("a notification makes no call"));

        gateway.ParseNotification(Hs256Jwt.Sign(ResultJson(respCode), Secret))!.Status.Should().Be(expected);
    }

    [Fact]
    public void A_notification_for_another_merchant_is_rejected_even_when_signed()
    {
        var (gateway, _) = Create((_, _) => throw new InvalidOperationException("a notification makes no call"));

        gateway.ParseNotification(Hs256Jwt.Sign(ResultJson("0000", merchantId: "OTHER"), Secret)).Should().BeNull();
    }

    [Fact]
    public async Task An_inquiry_result_for_another_merchant_is_rejected()
    {
        var (gateway, _) = Create((_, _) => SignedPayload(ResultJson("0000", merchantId: "OTHER")));

        var act = () => gateway.GetPaymentStatusAsync("INV1");

        await act.Should().ThrowAsync<PaymentGatewayException>().WithMessage("*merchant*");
    }
}
