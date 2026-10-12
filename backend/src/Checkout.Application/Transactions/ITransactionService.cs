using Checkout.Application.Common;
using Checkout.Application.Transactions.DTOs;

namespace Checkout.Application.Transactions;

/// <summary>A transaction for the result page. Status is never cached.</summary>
public interface ITransactionService
{
    /// <summary>Reads a transaction fresh from the database.</summary>
    /// <returns><see cref="ErrorCodes.TransactionNotFound"/> for an unknown invoice.</returns>
    Task<Result<TransactionDetails>> GetAsync(string invoiceNo, CancellationToken ct = default);

    /// <summary>
    /// Checks the payment status with the provider for a pending transaction and applies the result, then reads it. A
    /// final transaction, one the provider never opened a session for, or one inquired about within the
    /// throttle window, is returned as it is without calling the provider.
    /// </summary>
    /// <returns><see cref="ErrorCodes.TransactionNotFound"/> for an unknown invoice.</returns>
    Task<Result<TransactionDetails>> InquireAsync(string invoiceNo, CancellationToken ct = default);
}
