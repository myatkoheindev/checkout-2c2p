using Checkout.Domain.Exceptions;

namespace Checkout.Application.Abstractions.Payments;

/// <summary>
/// A payment provider as the use cases see it, in provider-neutral terms. Implemented in
/// Infrastructure over 2C2P (<c>External/TwoC2P/TwoC2PGateway</c>); mocked in tests. A second
/// provider is one more implementation in <c>External/&lt;Provider&gt;/</c> and one DI line.
/// </summary>
/// <remarks>
/// A payment is two steps: open a session, then (for a saved card) charge it. This matches how
/// providers work (2C2P Payment Token then Do Payment; Stripe PaymentIntent create then confirm),
/// and lets the session id be saved before any money moves. A refusal is a result, not an
/// exception; <see cref="PaymentGatewayException"/> means the provider could not be reached or
/// answered unreadably.
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>
    /// Opens a payment session. For a new payment method the result carries the hosted-page URL;
    /// for a saved card its <see cref="PaymentSession.SessionId"/> is passed to <see cref="ChargeSavedCardAsync"/>.
    /// </summary>
    /// <param name="request">The request, built by <see cref="PaymentSessionRequest.ForCheckout"/>.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <exception cref="PaymentGatewayException">The provider could not be reached or answered unreadably.</exception>
    Task<PaymentSession> CreateSessionAsync(PaymentSessionRequest request, CancellationToken ct = default);

    /// <summary>Charges a saved card in an open session, without a hosted page.</summary>
    /// <param name="request">The session, the saved card, and the 3DS return URL.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <exception cref="PaymentGatewayException">The provider could not be reached or answered unreadably.</exception>
    Task<SavedCardCharge> ChargeSavedCardAsync(SavedCardChargeRequest request, CancellationToken ct = default);

    /// <summary>Asks the provider for the current status of a payment.</summary>
    /// <param name="invoiceNo">The invoice number the session was opened with.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The verified result, in the same shape as a notification.</returns>
    /// <exception cref="PaymentGatewayException">The provider could not be reached, answered unreadably, or for another merchant.</exception>
    Task<PaymentResult> GetPaymentStatusAsync(string invoiceNo, CancellationToken ct = default);

    /// <summary>Verifies and reads a notification the provider posted to us.</summary>
    /// <param name="payload">The notification body's payload.</param>
    /// <returns>The result, or <c>null</c> when it cannot be trusted (bad signature, unreadable, another merchant).</returns>
    PaymentResult? ParseNotification(string payload);
}
