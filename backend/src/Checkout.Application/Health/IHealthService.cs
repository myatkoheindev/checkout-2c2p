namespace Checkout.Application.Health;

/// <summary>Reports the liveness of the service.</summary>
public interface IHealthService
{
    /// <summary>Gets the current liveness status.</summary>
    /// <returns>A snapshot taken at the moment of the call.</returns>
    HealthStatus GetStatus();
}
