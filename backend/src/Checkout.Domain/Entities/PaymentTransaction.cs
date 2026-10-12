using Checkout.Domain.Enums;

namespace Checkout.Domain.Entities;

/// <summary>
/// One attempt to charge a customer for a product. Stored in the <c>transaction</c> table; named
/// <c>PaymentTransaction</c> in code to avoid clashing with database transactions.
/// </summary>
/// <remarks>
/// Created <see cref="TransactionStatus.Pending"/> before 2C2P is called, and moved to a final
/// status exactly once — by the backend notification or by Payment Inquiry — through
/// the repository's conditional updates (<c>WHERE status = 'Pending'</c>).
/// </remarks>
public sealed class PaymentTransaction
{
    /// <summary>For EF Core.</summary>
    private PaymentTransaction()
    {
    }

    /// <summary>Starts a pending transaction for a checkout request.</summary>
    /// <param name="customerId">Who pays.</param>
    /// <param name="product">What is bought; its price and currency are copied.</param>
    /// <param name="customerCardId">The saved card, or <c>null</c> for a new payment method.</param>
    /// <param name="invoiceNo">The unique invoice number sent to 2C2P.</param>
    /// <param name="idempotencyKey">The client's <c>Idempotency-Key</c> header.</param>
    /// <param name="requestHash">A hash of the request body, to reject a reused key.</param>
    /// <param name="createdAt">Now, in UTC.</param>
    /// <returns>The pending transaction.</returns>
    public static PaymentTransaction StartPending(
        int customerId,
        Product product,
        int? customerCardId,
        string invoiceNo,
        string idempotencyKey,
        string requestHash,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new PaymentTransaction
        {
            CustomerId = customerId,
            ProductId = product.Id,
            CustomerCardId = customerCardId,
            Flow = customerCardId is null ? PaymentFlow.HostedPage : PaymentFlow.SavedCard,
            InvoiceNo = invoiceNo,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            Amount = product.Price,
            Currency = product.Currency,
            Status = TransactionStatus.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    public long Id { get; private set; }

    public int CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public int ProductId { get; private set; }

    public Product? Product { get; private set; }

    /// <summary>The saved card charged, for <see cref="PaymentFlow.SavedCard"/>.</summary>
    public int? CustomerCardId { get; private set; }

    public CustomerCard? CustomerCard { get; private set; }

    public PaymentFlow Flow { get; private set; }

    public string InvoiceNo { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>SHA-256 of the request body, so a reused key with a different body is rejected.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public TransactionStatus Status { get; private set; }

    /// <summary>The 2C2P payment token, kept for Do Payment and for diagnosis.</summary>
    public string? PaymentToken { get; private set; }

    /// <summary>
    /// Where the browser was sent (hosted page or 3DS). Stored so a repeated idempotency key gets
    /// the original response without calling 2C2P again.
    /// </summary>
    public string? RedirectUrl { get; private set; }

    /// <summary>From the notification's <c>channelCode</c>, e.g. <c>CC</c>.</summary>
    public string? PaymentChannel { get; private set; }

    /// <summary>From the notification's <c>accountNo</c>, e.g. <c>411111XXXXXX1111</c>.</summary>
    public string? MaskedCardNo { get; private set; }

    /// <summary>From the notification's <c>tranRef</c>.</summary>
    public string? ProviderRef { get; private set; }

    /// <summary>The last response code from 2C2P.</summary>
    public string? RespCode { get; private set; }

    /// <summary>The last response description from 2C2P.</summary>
    public string? RespDesc { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? PaidAt { get; private set; }

    /// <summary>Records the payment token and where to send the browser, if anywhere.</summary>
    /// <param name="paymentToken">The token from Payment Token.</param>
    /// <param name="redirectUrl">The hosted page or 3DS URL; <c>null</c> when no redirect is needed.</param>
    /// <param name="now">Now, in UTC.</param>
    public void AttachPaymentToken(string paymentToken, string? redirectUrl, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentToken);

        PaymentToken = paymentToken;
        RedirectUrl = redirectUrl;
        UpdatedAt = now;
    }

    /// <summary>Records a redirect issued after the payment token, such as a 3DS challenge.</summary>
    /// <param name="redirectUrl">Where to send the browser.</param>
    /// <param name="now">Now, in UTC.</param>
    public void SetRedirect(string redirectUrl, DateTime now)
    {
        RedirectUrl = redirectUrl;
        UpdatedAt = now;
    }
}
