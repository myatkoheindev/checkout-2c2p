using System.Net.Http.Json;
using System.Text.Json;
using Checkout.Infrastructure.External.TwoC2P;

namespace Checkout.Api.Tests.Support;

/// <summary>Builds and posts backend notifications the way 2C2P does.</summary>
public static class Notifications
{
    /// <summary>A signed notification body: <c>{ "payload": "&lt;JWT&gt;" }</c>.</summary>
    public static object Sign(object result, string secretKey = FakePaymentGateway.SecretKey) =>
        new { payload = Hs256Jwt.Sign(JsonSerializer.Serialize(result), secretKey) };

    /// <summary>A successful card payment, optionally saving the card.</summary>
    public static object Success(string invoiceNo, decimal amount, string? customerToken = null, string accountNo = "411111XXXXXX1111") => new
    {
        merchantID = "JT01",
        invoiceNo,
        accountNo,
        amount,
        currencyCode = "SGD",
        tranRef = "2868821",
        referenceNo = "2785703",
        approvalCode = "531484",
        eci = "05",
        transactionDateTime = "20261009143012",
        channelCode = "CC",
        customerToken,
        respCode = "0000",
        respDesc = "Success",
    };

    /// <summary>A declined payment.</summary>
    public static object Declined(string invoiceNo, decimal amount, string respCode = "4051", string respDesc = "Insufficient funds.") => new
    {
        merchantID = "JT01",
        invoiceNo,
        amount,
        currencyCode = "SGD",
        channelCode = "CC",
        respCode,
        respDesc,
    };

    public static Task<HttpResponseMessage> PostAsync(HttpClient http, object body) =>
        http.PostAsJsonAsync("/api/payments/2c2p/notification", body);
}
