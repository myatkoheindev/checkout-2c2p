using Checkout.Api.Contracts.Responses;
using Checkout.Api.Extensions;
using Checkout.Api.OpenApi;
using Checkout.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>The seeded products.</summary>
[ApiController]
[Route("api/products")]
[Tags("Products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>Handles <c>GET /api/products</c>.</summary>
    [HttpGet]
    [EndpointName("listProducts")]
    [EndpointSummary("List products")]
    [EndpointDescription(
        "The seeded products for the checkout dropdown, ordered by id. paymentType decides the " +
        "2C2P flow: OneTime offers every payment method, Recurring is card-only and saves the card. " +
        "Served from the Redis cache.")]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK, "application/json")]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.Products))]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await products.GetAllAsync(cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value!.Select(ProductResponse.From).ToList())
            : this.ToProblem(result);
    }
}
