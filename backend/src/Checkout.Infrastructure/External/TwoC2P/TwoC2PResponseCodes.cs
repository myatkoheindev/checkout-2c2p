namespace Checkout.Infrastructure.External.TwoC2P;

/// <summary>
/// The 2C2P codes the gateway translates into provider-neutral results. Every other code is a
/// failure; see https://developer.2c2p.com/docs/response-code-payment.
/// </summary>
internal static class TwoC2PResponseCodes
{
    /// <summary>Payment token issued, or payment successful (notification and inquiry).</summary>
    public const string Success = "0000";

    /// <summary>Do Payment: send the browser to the 3DS page in <c>data</c>.</summary>
    public const string RedirectRequired = "1001";

    /// <summary>Do Payment: processing finished; the result must be read with Payment Inquiry.</summary>
    public const string TransactionCompleted = "2000";

    /// <summary>The card channel code, for <c>paymentChannel</c> and Do Payment.</summary>
    public const string CardChannel = "CC";

    /// <summary>
    /// Inquiry and notification codes that mean "no outcome yet", so the transaction stays
    /// Pending rather than failing. The sandbox answers <c>2002</c> for an invoice the customer
    /// has not paid yet (still on the hosted page or in 3DS).
    /// </summary>
    public static readonly IReadOnlySet<string> NotFinished = new HashSet<string>(StringComparer.Ordinal)
    {
        "0001", // Transaction pending.
        "2001", // Transaction in progress.
        "2002", // Transaction not found.
        "4009", // Request in progress.
    };
}
