using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Checkout.Application.Abstractions.Payments;
using Checkout.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Checkout.Infrastructure.External.TwoC2P;

/// <summary>
/// <see cref="IPaymentGateway"/> over 2C2P Payment API v4.3.
/// </summary>
/// <remarks>
/// Payment Token and Payment Inquiry are JWT both ways (<c>{ "payload": "&lt;JWT&gt;" }</c>),
/// HS256 with the merchant secret key. Do Payment is plain JSON. A response whose signature does
/// not verify is never trusted: it raises <see cref="PaymentGatewayException"/>.
/// </remarks>
internal sealed partial class TwoC2PGateway(
    HttpClient http,
    IOptions<TwoC2POptions> options,
    ILogger<TwoC2PGateway> logger) : IPaymentGateway
{
    /// <summary>Relative to <see cref="TwoC2POptions.BaseUrl"/>.</summary>
    internal const string PaymentTokenPath = "payment/4.3/paymenttoken";

    /// <summary>Relative to <see cref="TwoC2POptions.BaseUrl"/>.</summary>
    internal const string DoPaymentPath = "payment/4.3/payment";

    /// <summary>Relative to <see cref="TwoC2POptions.BaseUrl"/>.</summary>
    internal const string PaymentInquiryPath = "payment/4.3/paymentinquiry";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private TwoC2POptions Settings => options.Value;

    /// <inheritdoc />
    /// <remarks>
    /// 2C2P Payment Token. The neutral flags map to: <c>CardOnly</c> → <c>paymentChannel: ["CC"]</c>;
    /// <c>OfferToSaveCard</c> → <c>tokenize: true</c>; <c>SavedCardToken</c> →
    /// <c>customerToken: [token]</c>, <c>customerTokenOnly: true</c>, <c>request3DS: "N"</c>.
    /// </remarks>
    public async Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var savedCard = request.SavedCardToken is not null;
        var payload = new PaymentTokenPayload(
            Settings.MerchantId,
            request.InvoiceNo,
            request.Description,
            decimal.Round(request.Amount, 2),
            request.Currency,
            request.ReturnUrl,
            request.NotificationUrl,
            Settings.Locale,
            request.CardOnly ? [TwoC2PResponseCodes.CardChannel] : null,
            request.OfferToSaveCard ? true : null,
            savedCard ? [request.SavedCardToken!] : null,
            savedCard ? true : null,
            savedCard ? "N" : null);

        var envelope = await PostSignedAsync(PaymentTokenPath, payload, request.InvoiceNo, ct).ConfigureAwait(false);

        // An error is sometimes answered without a payload, as a bare respCode/respDesc.
        if (envelope.Payload is null)
        {
            return new PaymentSession(false, null, null, Required(envelope.RespCode, "respCode"), envelope.RespDesc);
        }

        var response = VerifyAndDeserialize<PaymentTokenResponsePayload>(envelope.Payload, PaymentTokenPath);
        LogResponse(PaymentTokenPath, request.InvoiceNo, response.RespCode, response.RespDesc);

        var respCode = Required(response.RespCode, "respCode");
        var created = respCode == TwoC2PResponseCodes.Success && response.PaymentToken is not null;

        // A saved card is charged with Do Payment, so its hosted-page URL is never used.
        return new PaymentSession(
            created,
            response.PaymentToken,
            created && !savedCard ? response.WebPaymentUrl : null,
            respCode,
            response.RespDesc);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 2C2P Do Payment (plain JSON): <c>1001</c> with a URL → 3DS redirect; <c>2000</c> → submitted,
    /// confirm with Payment Inquiry; anything else → declined.
    /// </remarks>
    public async Task<SavedCardCharge> ChargeSavedCardAsync(SavedCardChargeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new DoPaymentBody(
            request.SessionId,
            Settings.Locale,
            request.ReturnUrl,
            request.ClientIp,
            new DoPaymentPayment(
                new DoPaymentCode(TwoC2PResponseCodes.CardChannel),
                new DoPaymentData(request.SavedCardToken)));

        using var httpResponse = await SendAsync(DoPaymentPath, JsonContent.Create(body, options: Json), ct).ConfigureAwait(false);
        var response = await ReadJsonAsync<DoPaymentResponseBody>(httpResponse, DoPaymentPath, ct).ConfigureAwait(false);
        LogResponse(DoPaymentPath, response.InvoiceNo, response.RespCode, response.RespDesc);

        var respCode = Required(response.RespCode, "respCode");
        var outcome = respCode switch
        {
            TwoC2PResponseCodes.RedirectRequired when !string.IsNullOrWhiteSpace(response.Data) => ChargeOutcome.RedirectRequired,
            TwoC2PResponseCodes.TransactionCompleted => ChargeOutcome.Submitted,
            _ => ChargeOutcome.Declined,
        };

        return new SavedCardCharge(
            outcome,
            outcome == ChargeOutcome.RedirectRequired ? response.Data : null,
            respCode,
            response.RespDesc);
    }

    /// <inheritdoc />
    /// <remarks>2C2P Payment Inquiry, JWT both ways.</remarks>
    public async Task<PaymentResult> GetPaymentStatusAsync(string invoiceNo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNo);

        var payload = new PaymentInquiryPayload(Settings.MerchantId, invoiceNo, Settings.Locale);
        var envelope = await PostSignedAsync(PaymentInquiryPath, payload, invoiceNo, ct).ConfigureAwait(false);

        if (envelope.Payload is null)
        {
            // No signed result to trust: report 2C2P's error as-is against the invoice we asked about.
            throw new PaymentGatewayException(
                $"Payment Inquiry for {invoiceNo} returned no payload (respCode {envelope.RespCode}: {envelope.RespDesc}).");
        }

        // For an invoice with no outcome yet, 2C2P answers 2002 with invoiceNo null and amount 0.
        var result = ToPaymentResult(VerifyAndDeserialize<PaymentResultPayload>(envelope.Payload, PaymentInquiryPath), invoiceNo);
        LogResponse(PaymentInquiryPath, result.InvoiceNo, result.ResponseCode, result.ResponseMessage);

        return result;
    }

    /// <inheritdoc />
    /// <remarks>2C2P backend notification: <c>{ "payload": "&lt;JWT&gt;" }</c>, HS256 with the merchant secret key.</remarks>
    public PaymentResult? ParseNotification(string payload)
    {
        if (!Hs256Jwt.TryVerify(payload, Settings.SecretKey, out var json))
        {
            LogBadSignature("backend notification");
            return null;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<PaymentResultPayload>(json, Json);
            return decoded is null ? null : ToPaymentResult(decoded, requestedInvoiceNo: null);
        }
        catch (Exception exception) when (exception is JsonException or PaymentGatewayException)
        {
            LogUnreadable("backend notification", exception);
            return null;
        }
    }

    private async Task<PayloadEnvelope> PostSignedAsync<TPayload>(
        string path,
        TPayload payload,
        string invoiceNo,
        CancellationToken cancellationToken)
    {
        var token = Hs256Jwt.Sign(JsonSerializer.Serialize(payload, Json), Settings.SecretKey);
        LogRequest(path, invoiceNo);

        using var response = await SendAsync(path, JsonContent.Create(new PayloadEnvelope(token), options: Json), cancellationToken).ConfigureAwait(false);

        return await ReadJsonAsync<PayloadEnvelope>(response, path, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.PostAsync(new Uri(path, UriKind.Relative), content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                response.Dispose();
                throw new PaymentGatewayException($"2C2P {path} answered HTTP {status}.");
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentGatewayException($"2C2P {path} could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PaymentGatewayException($"2C2P {path} timed out.", exception);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false)
                ?? throw new PaymentGatewayException($"2C2P {path} returned an empty body.");
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException($"2C2P {path} returned a body that is not valid JSON.", exception);
        }
    }

    private T VerifyAndDeserialize<T>(string token, string path)
    {
        if (!Hs256Jwt.TryVerify(token, Settings.SecretKey, out var json))
        {
            LogBadSignature(path);
            throw new PaymentGatewayException($"2C2P {path} returned a payload whose signature does not verify.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Json)
                ?? throw new PaymentGatewayException($"2C2P {path} returned an empty payload.");
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException($"2C2P {path} returned a payload that is not valid JSON.", exception);
        }
    }

    /// <summary>
    /// Maps a decoded result to the neutral shape. The result must be for our merchant. A success
    /// must carry its amount and currency, because they are checked against the stored
    /// transaction; a result with no outcome may omit them. The amount reported is the one the
    /// customer was charged, which is what the stored price is checked against.
    /// </summary>
    /// <param name="payload">The verified payload.</param>
    /// <param name="requestedInvoiceNo">For an inquiry, the invoice asked about, used when 2C2P omits it.</param>
    /// <exception cref="PaymentGatewayException">The result is for another merchant, or a required field is missing.</exception>
    private PaymentResult ToPaymentResult(PaymentResultPayload payload, string? requestedInvoiceNo)
    {
        // The signature already proves it came from someone holding our key; the merchant id
        // proves it is about our account.
        if (!string.Equals(payload.MerchantId, Settings.MerchantId, StringComparison.Ordinal))
        {
            throw new PaymentGatewayException($"2C2P result is for merchant '{payload.MerchantId}', not '{Settings.MerchantId}'.");
        }

        var respCode = Required(payload.RespCode, "respCode");
        var status = respCode == TwoC2PResponseCodes.Success
            ? PaymentStatus.Succeeded
            : TwoC2PResponseCodes.NotFinished.Contains(respCode) ? PaymentStatus.Pending : PaymentStatus.Failed;
        var isSuccess = status == PaymentStatus.Succeeded;

        // The amount the customer was charged: the FX amount when the merchant settles in another
        // currency (verified in the sandbox: SGD 35.00 charged, THB 848.24 settled).
        var charged = string.IsNullOrWhiteSpace(payload.FxCurrencyCode) || payload.FxAmount is null
            ? (Amount: payload.Amount, Currency: payload.CurrencyCode)
            : (Amount: payload.FxAmount, Currency: payload.FxCurrencyCode);

        return new PaymentResult(
            Required(string.IsNullOrWhiteSpace(payload.InvoiceNo) ? requestedInvoiceNo : payload.InvoiceNo, "invoiceNo"),
            status,
            isSuccess ? charged.Amount ?? throw new PaymentGatewayException("2C2P success is missing amount.") : charged.Amount ?? 0m,
            isSuccess ? Required(charged.Currency, "currencyCode") : charged.Currency ?? string.Empty,
            respCode,
            payload.RespDesc,
            payload.ChannelCode,
            payload.AccountNo,
            string.IsNullOrWhiteSpace(payload.TranRef) ? payload.ReferenceNo : payload.TranRef,
            string.IsNullOrWhiteSpace(payload.CustomerToken) ? null : payload.CustomerToken);
    }

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new PaymentGatewayException($"2C2P response is missing {name}.") : value;

    [LoggerMessage(Level = LogLevel.Information, Message = "2C2P {Path} request for invoice {InvoiceNo}.")]
    private partial void LogRequest(string path, string invoiceNo);

    [LoggerMessage(Level = LogLevel.Information, Message = "2C2P {Path} answered for invoice {InvoiceNo}: {RespCode} {RespDesc}.")]
    private partial void LogResponse(string path, string? invoiceNo, string? respCode, string? respDesc);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected a 2C2P {Source} whose JWT signature does not verify.")]
    private partial void LogBadSignature(string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected an unreadable 2C2P {Source}.")]
    private partial void LogUnreadable(string source, Exception exception);
}
