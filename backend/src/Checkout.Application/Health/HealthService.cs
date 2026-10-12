using Checkout.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Checkout.Application.Health;

/// <summary>Default <see cref="IHealthService"/>. Reads the clock through <see cref="TimeProvider"/>.</summary>
public sealed class HealthService(
    TimeProvider timeProvider,
    IHostEnvironment environment,
    IOptions<ApplicationOptions> options) : IHealthService
{
    /// <summary>The status reported while the process can answer.</summary>
    public const string Ok = "ok";

    /// <inheritdoc />
    public HealthStatus GetStatus() => new(
        Status: Ok,
        Service: options.Value.Name,
        Environment: environment.EnvironmentName,
        Time: timeProvider.GetUtcNow());
}
