namespace Checkout.Application.Abstractions.Payments;

/// <summary>What happened when a saved card was charged.</summary>
public enum ChargeOutcome
{
    /// <summary>The provider accepted the charge; read the final status with a status check.</summary>
    Submitted,

    /// <summary>The bank wants the customer to authenticate (3DS) at <see cref="SavedCardCharge.RedirectUrl"/>.</summary>
    RedirectRequired,

    /// <summary>The provider refused the charge.</summary>
    Declined,
}

/// <summary>The provider's answer to a saved-card charge.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="RedirectUrl">For <see cref="ChargeOutcome.RedirectRequired"/>, where to send the browser.</param>
/// <param name="ResponseCode">The provider's own response code, stored for diagnosis.</param>
/// <param name="ResponseMessage">The provider's description, shown on a decline.</param>
public sealed record SavedCardCharge(ChargeOutcome Outcome, string? RedirectUrl, string ResponseCode, string? ResponseMessage);
