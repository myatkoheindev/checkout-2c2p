using Checkout.Application.Checkouts.DTOs;
using Checkout.Domain.Enums;

namespace Checkout.Api.Contracts.Responses;

/// <summary>The answer to <c>POST /api/checkout</c>.</summary>
/// <param name="InvoiceNo">The invoice number sent to 2C2P; identifies the result page.</param>
/// <param name="Status"><c>Pending</c> until 2C2P reports an outcome; <c>Failed</c> if it refused at once.</param>
/// <param name="RedirectUrl">
/// Where to send the browser: the 2C2P hosted page, or a 3DS page for a saved card.
/// <c>null</c> when a saved-card payment needs no redirect: go straight to the result page.
/// </param>
public sealed record CheckoutResponse(string InvoiceNo, TransactionStatus Status, string? RedirectUrl)
{
    /// <summary>Maps the use-case result.</summary>
    public static CheckoutResponse From(CheckoutResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(result.InvoiceNo, result.Status, result.RedirectUrl);
    }
}
