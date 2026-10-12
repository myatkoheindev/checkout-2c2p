namespace Checkout.Domain.Enums;

/// <summary>How a product is paid for, which decides the 2C2P payment token request.</summary>
public enum PaymentType
{
    /// <summary>Any payment method the merchant has enabled.</summary>
    OneTime,

    /// <summary>Card only, saved as a customer token for later charges.</summary>
    Recurring,
}
