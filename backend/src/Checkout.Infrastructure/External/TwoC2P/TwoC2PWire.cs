using System.Text.Json.Serialization;

namespace Checkout.Infrastructure.External.TwoC2P;

// The JSON shapes 2C2P sends and receives, named exactly as its API does ("merchantID",
// "request3DS", "clientIP"). Kept apart from the Application types so a provider rename touches
// only this file.

/// <summary>The body of every JWT endpoint, both directions: <c>{ "payload": "&lt;JWT&gt;" }</c>.</summary>
internal sealed record PayloadEnvelope(
    [property: JsonPropertyName("payload")] string? Payload,
    [property: JsonPropertyName("respCode")] string? RespCode = null,
    [property: JsonPropertyName("respDesc")] string? RespDesc = null);

/// <summary>The signed Payment Token request.</summary>
internal sealed record PaymentTokenPayload(
    [property: JsonPropertyName("merchantID")] string MerchantId,
    [property: JsonPropertyName("invoiceNo")] string InvoiceNo,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currencyCode")] string CurrencyCode,
    [property: JsonPropertyName("frontendReturnUrl")] string FrontendReturnUrl,
    [property: JsonPropertyName("backendReturnUrl")] string BackendReturnUrl,
    [property: JsonPropertyName("locale")] string Locale,
    [property: JsonPropertyName("paymentChannel")] IReadOnlyList<string>? PaymentChannel,
    [property: JsonPropertyName("tokenize")] bool? Tokenize,
    [property: JsonPropertyName("customerToken")] IReadOnlyList<string>? CustomerToken,
    [property: JsonPropertyName("customerTokenOnly")] bool? CustomerTokenOnly,
    [property: JsonPropertyName("request3DS")] string? Request3DS);

/// <summary>The decoded Payment Token response.</summary>
internal sealed record PaymentTokenResponsePayload(
    [property: JsonPropertyName("webPaymentUrl")] string? WebPaymentUrl,
    [property: JsonPropertyName("paymentToken")] string? PaymentToken,
    [property: JsonPropertyName("respCode")] string? RespCode,
    [property: JsonPropertyName("respDesc")] string? RespDesc);

/// <summary>The plain-JSON Do Payment request.</summary>
internal sealed record DoPaymentBody(
    [property: JsonPropertyName("paymentToken")] string PaymentToken,
    [property: JsonPropertyName("locale")] string Locale,
    [property: JsonPropertyName("responseReturnUrl")] string ResponseReturnUrl,
    [property: JsonPropertyName("clientIP")] string? ClientIp,
    [property: JsonPropertyName("payment")] DoPaymentPayment Payment);

internal sealed record DoPaymentPayment(
    [property: JsonPropertyName("code")] DoPaymentCode Code,
    [property: JsonPropertyName("data")] DoPaymentData Data);

internal sealed record DoPaymentCode([property: JsonPropertyName("channelCode")] string ChannelCode);

internal sealed record DoPaymentData([property: JsonPropertyName("customerToken")] string CustomerToken);

/// <summary>The plain-JSON Do Payment response.</summary>
internal sealed record DoPaymentResponseBody(
    [property: JsonPropertyName("channelCode")] string? ChannelCode,
    [property: JsonPropertyName("invoiceNo")] string? InvoiceNo,
    [property: JsonPropertyName("data")] string? Data,
    [property: JsonPropertyName("respCode")] string? RespCode,
    [property: JsonPropertyName("respDesc")] string? RespDesc);

/// <summary>The signed Payment Inquiry request.</summary>
internal sealed record PaymentInquiryPayload(
    [property: JsonPropertyName("merchantID")] string MerchantId,
    [property: JsonPropertyName("invoiceNo")] string InvoiceNo,
    [property: JsonPropertyName("locale")] string Locale);

/// <summary>
/// The decoded backend notification and Payment Inquiry response, which share their fields.
/// <c>amount</c> is read from a JSON number or a string, since samples show both.
/// </summary>
/// <remarks>
/// With multi-currency pricing, <c>amount</c>/<c>currencyCode</c> are what the merchant settles
/// (the sandbox's JT01 settles in THB) and <c>fxAmount</c>/<c>fxCurrencyCode</c> are what the
/// customer was charged, in the currency we asked for.
/// </remarks>
internal sealed record PaymentResultPayload(
    [property: JsonPropertyName("merchantID")] string? MerchantId,
    [property: JsonPropertyName("invoiceNo")] string? InvoiceNo,
    [property: JsonPropertyName("amount"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? Amount,
    [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
    [property: JsonPropertyName("fxAmount"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? FxAmount,
    [property: JsonPropertyName("fxCurrencyCode")] string? FxCurrencyCode,
    [property: JsonPropertyName("channelCode")] string? ChannelCode,
    [property: JsonPropertyName("accountNo")] string? AccountNo,
    [property: JsonPropertyName("tranRef")] string? TranRef,
    [property: JsonPropertyName("referenceNo")] string? ReferenceNo,
    [property: JsonPropertyName("customerToken")] string? CustomerToken,
    [property: JsonPropertyName("respCode")] string? RespCode,
    [property: JsonPropertyName("respDesc")] string? RespDesc);
