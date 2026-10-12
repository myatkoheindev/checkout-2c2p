using Checkout.Domain.Enums;

namespace Checkout.Application.Transactions.DTOs;

/// <summary>Everything the result page shows, read fresh from MySQL.</summary>
/// <param name="InvoiceNo">The invoice number.</param>
/// <param name="Status">Pending, Succeeded, or Failed.</param>
/// <param name="Amount">The amount charged.</param>
/// <param name="Currency">The currency.</param>
/// <param name="CustomerId">For "Go back", to preselect the customer.</param>
/// <param name="CustomerName">The payer.</param>
/// <param name="ProductId">For "Go back", to preselect the product.</param>
/// <param name="ProductName">What was bought.</param>
/// <param name="PaymentType">OneTime or Recurring.</param>
/// <param name="Flow">Hosted page or saved card.</param>
/// <param name="PaymentChannel">How it was paid, e.g. <c>CC</c>, once known.</param>
/// <param name="MaskedCardNo">The masked card or account number, once known.</param>
/// <param name="RespCode">The payment provider's last response code.</param>
/// <param name="RespDesc">The payment provider's last description; the failure reason on a failed payment.</param>
/// <param name="CreatedAt">When checkout started, UTC.</param>
/// <param name="PaidAt">When it was paid, UTC.</param>
public sealed record TransactionDetails(
    string InvoiceNo,
    TransactionStatus Status,
    decimal Amount,
    string Currency,
    int CustomerId,
    string CustomerName,
    int ProductId,
    string ProductName,
    PaymentType PaymentType,
    PaymentFlow Flow,
    string? PaymentChannel,
    string? MaskedCardNo,
    string? RespCode,
    string? RespDesc,
    DateTime CreatedAt,
    DateTime? PaidAt);
