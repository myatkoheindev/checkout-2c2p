using Checkout.Application.Abstractions.Payments;

namespace Checkout.Application.Payments;

/// <summary>
/// Moves transactions to their final status. The only way a transaction becomes Succeeded, used
/// by the provider notification, a status check, and the checkout itself.
/// </summary>
public interface IPaymentConfirmationService
{
    /// <summary>
    /// Applies a verified provider result to its transaction, and saves a newly saved card.
    /// Idempotent: a repeated or late result changes nothing once the transaction is final.
    /// </summary>
    Task<ConfirmationOutcome> ApplyAsync(PaymentResult result, CancellationToken ct = default);

    /// <summary>Marks a pending transaction Failed. Does nothing if it is already final.</summary>
    /// <returns><c>true</c> if this call changed the status.</returns>
    Task<bool> MarkFailedAsync(string invoiceNo, string respCode, string? respDesc, CancellationToken ct = default);
}
