using Checkout.Api.Contracts.Responses;
using Checkout.Api.Extensions;
using Checkout.Api.OpenApi;
using Checkout.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>A transaction's status for the result page.</summary>
[ApiController]
[Route("api/transactions")]
[Tags("Transactions")]
public sealed class TransactionsController(ITransactionService transactions) : ControllerBase
{
    /// <summary>Handles <c>GET /api/transactions/{invoiceNo}</c>.</summary>
    [HttpGet("{invoiceNo}")]
    [EndpointName("getTransaction")]
    [EndpointSummary("Get a transaction")]
    [EndpointDescription(
        "Status and details for the result page, read fresh from MySQL on every call — never " +
        "cached, because the browser can return before or after the backend notification. Poll " +
        "while status is Pending.")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemExamples.ContentType)]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.Transaction))]
    [ResponseExample(StatusCodes.Status404NotFound, nameof(ApiExamples.TransactionNotFound), ProblemExamples.ContentType)]
    public async Task<ActionResult<TransactionResponse>> Get(string invoiceNo, CancellationToken cancellationToken)
    {
        var result = await transactions.GetAsync(invoiceNo, cancellationToken);

        return result.IsSuccess ? Ok(TransactionResponse.From(result.Value!)) : this.ToProblem(result);
    }

    /// <summary>Handles <c>POST /api/transactions/{invoiceNo}/inquiry</c>.</summary>
    [HttpPost("{invoiceNo}/inquiry")]
    [EndpointName("inquireTransaction")]
    [EndpointSummary("Confirm a pending payment with 2C2P")]
    [EndpointDescription(
        "Runs 2C2P Payment Inquiry for a Pending transaction and applies the verified result, then " +
        "returns the transaction. Call it when the browser returns from 3DS for a saved card, or " +
        "as a fallback when a hosted-page notification is late. A final transaction is returned " +
        "without calling 2C2P, and calls are throttled to one per invoice every few seconds. While " +
        "2C2P has no outcome yet (e.g. 2002), the transaction stays Pending.")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemExamples.ContentType)]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.Transaction))]
    [ResponseExample(StatusCodes.Status404NotFound, nameof(ApiExamples.InquiryNotFound), ProblemExamples.ContentType)]
    public async Task<ActionResult<TransactionResponse>> Inquire(string invoiceNo, CancellationToken cancellationToken)
    {
        var result = await transactions.InquireAsync(invoiceNo, cancellationToken);

        return result.IsSuccess ? Ok(TransactionResponse.From(result.Value!)) : this.ToProblem(result);
    }
}
