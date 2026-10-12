using Checkout.Api.Contracts.Requests;
using Checkout.Api.Contracts.Responses;
using Checkout.Api.Extensions;
using Checkout.Api.OpenApi;
using Checkout.Application.Checkouts;
using Checkout.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary><c>POST /api/checkout</c>: start a payment, at most once per idempotency key.</summary>
[ApiController]
[Route("api/checkout")]
[Tags("Checkout")]
public sealed class CheckoutController(ICheckoutService checkout) : ControllerBase
{
    /// <summary>The request header that identifies one purchase across retries.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Set to <c>true</c> on a response that replays an earlier request's result.</summary>
    public const string ReplayedHeader = "Idempotent-Replayed";

    /// <summary>Handles <c>POST /api/checkout</c>.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [EndpointName("checkout")]
    [EndpointSummary("Start a payment")]
    [EndpointDescription(
        "Creates a Pending transaction and starts the 2C2P flow. With no customerCardId the " +
        "response carries the 2C2P hosted page as redirectUrl (OneTime: every method; Recurring: " +
        "card only, with the save-card option). With a customerCardId the saved card is charged " +
        "through the Direct API: redirectUrl is a 3DS page when the bank asks for one, otherwise " +
        "null — go straight to the result page.\n\n" +
        "**Idempotency.** Send an Idempotency-Key (a UUID generated when the checkout page " +
        "loads) and reuse it for every retry of the same purchase. A repeated key returns the " +
        "original invoiceNo and redirectUrl without calling 2C2P again (header " +
        "`Idempotent-Replayed: true`). Concurrent requests with one key run one at a time; one " +
        "that cannot get its turn within a few seconds receives 409. The same key with a " +
        "different body is rejected with 422.")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemExamples.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemExamples.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemExamples.ContentType)]
    [RequestExample(nameof(ApiExamples.Checkout))]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.CheckoutStarted))]
    [ResponseExample(StatusCodes.Status400BadRequest, nameof(ApiExamples.CheckoutMissingKey), ProblemExamples.ContentType)]
    [ResponseExample(StatusCodes.Status409Conflict, nameof(ApiExamples.CheckoutInProgress), ProblemExamples.ContentType)]
    [ResponseExample(StatusCodes.Status422UnprocessableEntity, nameof(ApiExamples.CheckoutInvalidCard), ProblemExamples.ContentType)]
    public async Task<ActionResult<CheckoutResponse>> Post(
        CheckoutRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(idempotencyKey, out var key))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The {IdempotencyKeyHeader} header is required and must be a UUID.");
        }

        var result = await checkout.CheckoutAsync(
            request.ToCommand(),
            key.ToString("D"),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (result.IsSuccess)
        {
            if (result.Value!.Replayed)
            {
                Response.Headers[ReplayedHeader] = "true";
            }

            return Ok(CheckoutResponse.From(result.Value));
        }

        // An id in the body that does not resolve is a field error (422), not a missing resource.
        return result.ErrorCode switch
        {
            ErrorCodes.CustomerNotFound => this.FieldProblem("customerId", result.Error!),
            ErrorCodes.ProductNotFound => this.FieldProblem("productId", result.Error!),
            ErrorCodes.CardNotFound => this.FieldProblem("customerCardId", result.Error!),
            _ => this.ToProblem(result),
        };
    }
}
