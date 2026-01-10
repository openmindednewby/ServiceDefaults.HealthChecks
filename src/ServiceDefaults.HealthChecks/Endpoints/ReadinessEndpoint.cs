using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ServiceDefaults.HealthChecks.Endpoints;

/// <summary>
/// FastEndpoint for readiness health check - visible in Swagger.
/// </summary>
public class ReadinessEndpoint : EndpointWithoutRequest<HealthCheckResponse>
{
    private readonly HealthCheckService _healthCheckService;

    public ReadinessEndpoint(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public override void Configure()
    {
        Get("/health/ready");
        AllowAnonymous();
        Tags("Health");
        Summary(s =>
        {
            s.Summary = "Readiness Check";
            s.Description = "Check if the application is ready to handle traffic. Includes database connectivity and startup state. Used by Kubernetes readiness probes.";
            s.Response<HealthCheckResponse>(200, "Application is ready");
            s.Response<HealthCheckResponse>(503, "Application is not ready");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await _healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains(HealthCheckExtensions.ReadyTag),
            ct);

        var response = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(e => new HealthCheckEntry
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description,
                Exception = e.Value.Exception?.Message
            }).ToList()
        };

        if (report.Status == HealthStatus.Healthy)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
    }
}
