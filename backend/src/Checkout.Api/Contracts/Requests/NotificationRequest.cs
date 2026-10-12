namespace Checkout.Api.Contracts.Requests;

/// <summary>What 2C2P posts to <c>backendReturnUrl</c>: one JWT, signed HS256 with the merchant secret key.</summary>
/// <param name="Payload">The signed payment result.</param>
public sealed record NotificationRequest(string? Payload);
