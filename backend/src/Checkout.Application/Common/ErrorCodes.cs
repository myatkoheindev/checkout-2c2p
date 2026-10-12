namespace Checkout.Application.Common;

/// <summary>Every <see cref="Result{T}.ErrorCode"/> the services return.</summary>
public static class ErrorCodes
{
    /// <summary>The request failed validation; see <see cref="Result{T}.ValidationErrors"/>.</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";

    public const string ProductNotFound = "PRODUCT_NOT_FOUND";

    /// <summary>The card does not exist or belongs to another customer.</summary>
    public const string CardNotFound = "CARD_NOT_FOUND";

    public const string TransactionNotFound = "TRANSACTION_NOT_FOUND";

    /// <summary>The idempotency key was used for a different checkout body.</summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";

    /// <summary>A checkout with this idempotency key is still running.</summary>
    public const string CheckoutInProgress = "CHECKOUT_IN_PROGRESS";

    /// <summary>A provider notification that could not be verified (bad signature, unreadable, or another merchant).</summary>
    public const string InvalidSignature = "INVALID_SIGNATURE";
}
