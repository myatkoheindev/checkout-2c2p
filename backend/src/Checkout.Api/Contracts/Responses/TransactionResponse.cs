using Checkout.Application.Transactions.DTOs;
using Checkout.Domain.Enums;

namespace Checkout.Api.Contracts.Responses;

/// <summary>A transaction as the result page shows it.</summary>
/// <param name="InvoiceNo">The invoice number.</param>
/// <param name="Status">Pending, Succeeded or Failed.</param>
/// <param name="Amount">The amount charged.</param>
/// <param name="Currency">The currency.</param>
/// <param name="CustomerId">Who paid.</param>
/// <param name="CustomerName">The customer's full name.</param>
/// <param name="ProductId">What was bought.</param>
/// <param name="ProductName">The product's name.</param>
/// <param name="PaymentType">OneTime or Recurring.</param>
/// <param name="Flow">HostedPage or SavedCard.</param>
/// <param name="PaymentChannel">How it was paid, e.g. the card scheme.</param>
/// <param name="MaskedCardNo">The masked card number.</param>
/// <param name="RespCode">2C2P's last response code.</param>
/// <param name="RespDesc">2C2P's last description; the failure reason on a failed payment.</param>
/// <param name="CreatedAt">When checkout started, in UTC.</param>
/// <param name="PaidAt">When it succeeded, in UTC.</param>
public sealed record TransactionResponse(
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
    DateTime? PaidAt)
{
    /// <summary>Maps the use-case result.</summary>
    public static TransactionResponse From(TransactionDetails t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new(
            t.InvoiceNo,
            t.Status,
            t.Amount,
            t.Currency,
            t.CustomerId,
            t.CustomerName,
            t.ProductId,
            t.ProductName,
            t.PaymentType,
            t.Flow,
            t.PaymentChannel,
            t.MaskedCardNo,
            t.RespCode,
            t.RespDesc,
            t.CreatedAt,
            t.PaidAt);
    }
}
