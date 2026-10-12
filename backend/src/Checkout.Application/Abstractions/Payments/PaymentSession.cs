namespace Checkout.Application.Abstractions.Payments;

/// <summary>The provider's answer when a payment starts, after its signature was verified.</summary>
/// <param name="IsCreated">Whether the provider opened a payment session.</param>
/// <param name="SessionId">The provider's id for this payment; needed to charge a saved card.</param>
/// <param name="RedirectUrl">The hosted payment page for a new payment method; <c>null</c> for a saved card.</param>
/// <param name="ResponseCode">The provider's own response code, stored for diagnosis.</param>
/// <param name="ResponseMessage">The provider's description, shown when the payment could not start.</param>
public sealed record PaymentSession(
    bool IsCreated,
    string? SessionId,
    string? RedirectUrl,
    string ResponseCode,
    string? ResponseMessage);
