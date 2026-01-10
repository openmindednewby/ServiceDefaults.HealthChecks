using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceDefaults.HealthChecks;

/// <summary>
/// Health check that reports unhealthy until <see cref="StartupState.MarkReady"/> is called.
/// </summary>
public sealed class StartupHealthCheck : IHealthCheck
{
    private readonly StartupState _startupState;

    public StartupHealthCheck(StartupState startupState)
    {
        _startupState = startupState;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_startupState.IsReady
            ? HealthCheckResult.Healthy("Application startup complete")
            : HealthCheckResult.Unhealthy("Application is still starting up"));
    }
}
