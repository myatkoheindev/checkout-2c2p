namespace Checkout.Application.Abstractions.Payments;

/// <summary>Where a payment stands, as the provider reports it.</summary>
public enum PaymentStatus
{
    /// <summary>The customer was charged.</summary>
    Succeeded,

    /// <summary>The payment failed or was declined.</summary>
    Failed,

    /// <summary>No outcome yet (the customer may still be paying); the transaction stays Pending.</summary>
    Pending,
}

/// <summary>
/// The final word on a payment, from a provider notification or a status check. Both carry the
/// same fields, so one shape and one set of rules serve both.
/// </summary>
/// <param name="InvoiceNo">Identifies the transaction.</param>
/// <param name="Status">What the provider says happened.</param>
/// <param name="Amount">
/// The amount the customer was charged; must match what was stored. <c>0</c> when there is no
/// outcome yet.
/// </param>
/// <param name="Currency">The currency the customer was charged; must match what was stored. Empty when there is no outcome yet.</param>
/// <param name="ResponseCode">The provider's own response code, stored for diagnosis.</param>
/// <param name="ResponseMessage">The provider's description, shown on failure.</param>
/// <param name="PaymentMethod">How it was paid, e.g. the card scheme.</param>
/// <param name="MaskedCardNo">The masked card or account number.</param>
/// <param name="ProviderReference">The provider's reference for the payment.</param>
/// <param name="SavedCardToken">A newly saved card's token, when the customer chose to save it.</param>
public sealed record PaymentResult(
    string InvoiceNo,
    PaymentStatus Status,
    decimal Amount,
    string Currency,
    string ResponseCode,
    string? ResponseMessage,
    string? PaymentMethod,
    string? MaskedCardNo,
    string? ProviderReference,
    string? SavedCardToken);
