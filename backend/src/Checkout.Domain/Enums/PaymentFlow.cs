namespace Checkout.Domain.Enums;

/// <summary>Which 2C2P integration a transaction goes through.</summary>
public enum PaymentFlow
{
    /// <summary>Redirect to the 2C2P hosted page; confirmed by the backend notification.</summary>
    HostedPage,

    /// <summary>Direct API with a customer token; confirmed by Payment Inquiry.</summary>
    SavedCard,
}
