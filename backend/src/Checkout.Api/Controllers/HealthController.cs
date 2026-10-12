using Checkout.Api.Contracts.Responses;
using Checkout.Api.OpenApi;
using Checkout.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.Controllers;

/// <summary>The liveness probe, and the reference shape for a controller: bind, call one service, map.</summary>
[ApiController]
[Route("api/health")]
[Tags("System")]
public sealed class HealthController(IHealthService health) : ControllerBase
{
    /// <summary>Handles <c>GET /api/health</c>.</summary>
    [HttpGet]
    [EndpointName("health")]
    [EndpointSummary("Liveness probe")]
    [EndpointDescription("Reports that the API process is up. Touches no dependency.")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK, "application/json")]
    [ResponseExample(StatusCodes.Status200OK, nameof(ApiExamples.Health))]
    public ActionResult<HealthResponse> Get() => Ok(HealthResponse.From(health.GetStatus()));
}
