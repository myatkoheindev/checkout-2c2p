using Checkout.Application.Common;

namespace Checkout.Application.Payments;

/// <summary>Handles the payment provider's notification, the source of truth for a hosted-page payment.</summary>
public interface IPaymentNotificationService
{
    /// <summary>Verifies the notification's JWT and applies its result.</summary>
    /// <param name="payload">The signed payload the provider posted.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>
    /// <see cref="ErrorCodes.InvalidSignature"/> when the token is invalid (reject it); otherwise what
    /// was done, which is acknowledged with 200 even when nothing changed, so the provider stops retrying.
    /// </returns>
    Task<Result<ConfirmationOutcome>> HandleAsync(string? payload, CancellationToken ct = default);
}
