namespace Checkout.Api.Tests.Support;

/// <summary>Creates a Pending hosted-page transaction to settle.</summary>
public static class Checkouts
{
    /// <summary>Checks out a seeded product with a new payment method; returns its invoice number.</summary>
    public static async Task<string> StartHostedAsync(HttpClient http, int customerId, int productId)
    {
        var response = await CheckoutClient.PostAsync(http, new { customerId, productId }, Guid.NewGuid().ToString());
        response.Body.Should().NotBeNull(response.Raw);

        return response.Body!.InvoiceNo;
    }
}
