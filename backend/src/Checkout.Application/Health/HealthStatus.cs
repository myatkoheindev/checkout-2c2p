namespace Checkout.Application.Health;

/// <summary>The liveness payload: whether the service is up, what it is, and where it runs.</summary>
/// <param name="Status">Always <c>ok</c> while the process can answer.</param>
/// <param name="Service">The configured application name.</param>
/// <param name="Environment">The hosting environment, such as <c>Development</c>.</param>
/// <param name="Time">The server's current time.</param>
public sealed record HealthStatus(string Status, string Service, string Environment, DateTimeOffset Time);
