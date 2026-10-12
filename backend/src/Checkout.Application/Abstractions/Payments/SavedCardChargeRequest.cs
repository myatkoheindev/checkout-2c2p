namespace Checkout.Application.Abstractions.Payments;

/// <summary>A charge to a saved card, inside a session opened by the gateway.</summary>
/// <param name="SessionId">From <see cref="PaymentSession.SessionId"/>.</param>
/// <param name="SavedCardToken">The saved card's token.</param>
/// <param name="ReturnUrl">Where the browser returns after a 3DS challenge.</param>
/// <param name="ClientIp">The shopper's IP address, when known.</param>
public sealed record SavedCardChargeRequest(string SessionId, string SavedCardToken, string ReturnUrl, string? ClientIp = null);
