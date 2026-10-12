using Checkout.Domain.Enums;

namespace Checkout.Application.Abstractions.Payments;

/// <summary>
/// What to ask the payment provider for when a payment starts, in provider-neutral terms. Each
/// gateway translates these flags into its own request fields.
/// </summary>
/// <param name="InvoiceNo">The unique invoice number; the provider echoes it in every response.</param>
/// <param name="Description">What is being paid for, at most <see cref="MaxDescriptionLength"/> characters.</param>
/// <param name="Amount">The amount to charge.</param>
/// <param name="Currency">ISO 4217 code; always <c>SGD</c> here.</param>
/// <param name="ReturnUrl">Where the provider sends the browser afterwards.</param>
/// <param name="NotificationUrl">Where the provider posts the server-to-server result.</param>
/// <param name="CardOnly">Offer card payment only, instead of every method the merchant has.</param>
/// <param name="OfferToSaveCard">Let the customer save the card for future payments.</param>
/// <param name="SavedCardToken">Pay with this saved card only; <c>null</c> for a new payment method.</param>
public sealed record PaymentSessionRequest(
    string InvoiceNo,
    string Description,
    decimal Amount,
    string Currency,
    string ReturnUrl,
    string NotificationUrl,
    bool CardOnly = false,
    bool OfferToSaveCard = false,
    string? SavedCardToken = null)
{
    /// <summary>The longest description accepted.</summary>
    public const int MaxDescriptionLength = 250;

    /// <summary>
    /// Builds the request for a checkout, applying the payment rules:
    /// <list type="bullet">
    /// <item>OneTime, new method — every payment method the merchant has.</item>
    /// <item>Recurring, new method — card only, with the option to save the card.</item>
    /// <item>Saved card, any product — card only, that saved card only.</item>
    /// </list>
    /// </summary>
    /// <param name="paymentType">The product's payment type.</param>
    /// <param name="invoiceNo">The transaction's invoice number.</param>
    /// <param name="description">The product name.</param>
    /// <param name="amount">The amount to charge.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="returnUrl">The browser return URL.</param>
    /// <param name="notificationUrl">The server-to-server notification URL.</param>
    /// <param name="savedCardToken">The saved card's token, or <c>null</c> for a new payment method.</param>
    /// <returns>The request.</returns>
    public static PaymentSessionRequest ForCheckout(
        PaymentType paymentType,
        string invoiceNo,
        string description,
        decimal amount,
        string currency,
        string returnUrl,
        string notificationUrl,
        string? savedCardToken)
    {
        ArgumentNullException.ThrowIfNull(description);

        var common = new PaymentSessionRequest(
            invoiceNo,
            description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description,
            amount,
            currency,
            returnUrl,
            notificationUrl);

        if (savedCardToken is not null)
        {
            return common with { CardOnly = true, SavedCardToken = savedCardToken };
        }

        return paymentType switch
        {
            PaymentType.Recurring => common with { CardOnly = true, OfferToSaveCard = true },
            _ => common,
        };
    }
}
