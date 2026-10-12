using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Checkout.Domain.Enums;

namespace Checkout.Api.Tests.Support;

/// <summary>Posts to <c>/api/checkout</c> the way the frontend does.</summary>
public static class CheckoutClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public sealed record Response(HttpStatusCode Status, Body? Body, bool Replayed, string Raw);

    public sealed record Body(string InvoiceNo, TransactionStatus Status, string? RedirectUrl);

    public static async Task<Response> PostAsync(HttpClient http, object body, string? idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(http);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        return new Response(
            response.StatusCode,
            response.StatusCode == HttpStatusCode.OK ? JsonSerializer.Deserialize<Body>(raw, Json) : null,
            response.Headers.TryGetValues("Idempotent-Replayed", out var replayed) && replayed.Contains("true"),
            raw);
    }
}
