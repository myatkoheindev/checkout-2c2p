using Checkout.Domain.Enums;

namespace Checkout.Application.Checkouts.DTOs;

/// <summary>The answer to <c>POST /api/checkout</c>.</summary>
/// <param name="InvoiceNo">The invoice number sent to the payment provider; identifies the result page.</param>
/// <param name="Status"><c>Pending</c> until the payment provider reports an outcome; <c>Failed</c> if it refused at once.</param>
/// <param name="RedirectUrl">
/// Where to send the browser: the provider's hosted page, or a 3DS page for a saved card.
/// <c>null</c> when a saved-card payment needs no redirect — go straight to the result page.
/// </param>
public sealed record CheckoutResult(string InvoiceNo, TransactionStatus Status, string? RedirectUrl)
{
    /// <summary>Whether this is the stored result of an earlier request with the same key. Sent as a header, not in the body.</summary>
    public bool Replayed { get; init; }
}
