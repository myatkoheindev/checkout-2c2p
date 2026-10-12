using Checkout.Api.Contracts.Responses;
using Checkout.Api.Extensions;
using Checkout.Api.OpenApi;
using Checkout.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>The seeded customers and their saved cards.</summary>
[ApiController]
[Route("api/customers")]
[Tags("Customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    /// <summary>Handles <c>GET /api/customers</c>.</summary>
    [HttpGet]
    [EndpointName("listCustomers")]
    [EndpointSummary("List customers")]
    [EndpointDescription("The seeded customers for the checkout dropdown, ordered by id. Served from the Redis cache.")]
    [ProducesResponseType<IReadOnlyList<CustomerResponse>>(StatusCodes.Status200OK, "application/json")]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.Customers))]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await customers.GetAllAsync(cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value!.Select(CustomerResponse.From).ToList())
            : this.ToProblem(result);
    }

    /// <summary>Handles <c>GET /api/customers/{id}/cards</c>.</summary>
    [HttpGet("{id:int}/cards")]
    [EndpointName("listCustomerCards")]
    [EndpointSummary("List a customer's saved cards")]
    [EndpointDescription(
        "Cards saved through 2C2P tokenization, newest first, with masked numbers only. " +
        "Pass a card's id as customerCardId at checkout to pay without the hosted page.")]
    [ProducesResponseType<IReadOnlyList<SavedCardResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemExamples.ContentType)]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.SavedCards))]
    [ResponseExample(StatusCodes.Status404NotFound, nameof(ApiExamples.CustomerNotFound), ProblemExamples.ContentType)]
    public async Task<ActionResult<IReadOnlyList<SavedCardResponse>>> GetSavedCards(int id, CancellationToken cancellationToken)
    {
        var result = await customers.GetSavedCardsAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value!.Select(SavedCardResponse.From).ToList())
            : this.ToProblem(result);
    }
}
