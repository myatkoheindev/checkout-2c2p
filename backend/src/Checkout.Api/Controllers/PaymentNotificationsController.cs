using Checkout.Api.Contracts.Requests;
using Checkout.Api.Extensions;
using Checkout.Api.OpenApi;
using Checkout.Application.Payments;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>The 2C2P backend notification (<c>backendReturnUrl</c>).</summary>
[ApiController]
[Route("api/payments/2c2p/notification")]
[Tags("Payments")]
public sealed class PaymentNotificationsController(IPaymentNotificationService notifications) : ControllerBase
{
    /// <summary>Handles <c>POST /api/payments/2c2p/notification</c>.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [EndpointName("receive2c2pNotification")]
    [EndpointSummary("2C2P backend notification")]
    [EndpointDescription(
        "Called by 2C2P, not by the frontend. The source of truth for a hosted-page payment.\n\n" +
        "The payload's JWT signature is verified with the merchant secret key (HS256) and its " +
        "merchantID must be ours; anything that fails is rejected with 400. The transaction is found by " +
        "invoiceNo, its amount and currency are checked, and it moves from Pending to Succeeded " +
        "(respCode 0000) or Failed — only while still Pending, so a repeated notification changes " +
        "nothing. A customerToken is saved as a card once. Every verified notification is answered " +
        "200, so 2C2P stops retrying.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemExamples.ContentType)]
    [RequestExample(nameof(ApiExamples.Notification))]
    [ResponseExample(StatusCodes.Status400BadRequest, nameof(ApiExamples.NotificationRejected), ProblemExamples.ContentType)]
    public async Task<ActionResult> Post(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await notifications.HandleAsync(request.Payload, cancellationToken);

        return result.IsSuccess ? Ok() : this.ToProblem(result);
    }
}
