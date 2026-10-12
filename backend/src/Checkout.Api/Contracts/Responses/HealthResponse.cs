using Checkout.Application.Health;

namespace Checkout.Api.Contracts.Responses;

/// <summary>The liveness answer.</summary>
/// <param name="Status">Always <c>ok</c> while the process can answer.</param>
/// <param name="Service">The service name.</param>
/// <param name="Environment">The hosting environment.</param>
/// <param name="Time">The server time.</param>
public sealed record HealthResponse(string Status, string Service, string Environment, DateTimeOffset Time)
{
    /// <summary>Maps the use-case result.</summary>
    public static HealthResponse From(HealthStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new(status.Status, status.Service, status.Environment, status.Time);
    }
}
