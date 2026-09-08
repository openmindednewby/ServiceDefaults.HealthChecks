using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace ServiceDefaults.HealthChecks;

/// <summary>
/// Extension methods for configuring production-ready health checks with separate
/// liveness, readiness, and startup concerns.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Tag for liveness health checks. These checks verify the app is running.
    /// </summary>
    public const string LiveTag = "live";

    /// <summary>
    /// Tag for readiness health checks. These checks verify the app can handle traffic.
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Tag for startup health checks. These checks verify startup/initialization has completed.
    /// </summary>
    public const string StartTag = "start";

    /// <summary>
    /// Adds core health check services with liveness check.
    /// Database readiness lives in a separate package so services without a
    /// database do not take a driver dependency; see Dloizides.HealthChecks.Npgsql.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder AddHealthCheckDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Register StartupState as singleton for startup-aware readiness
        builder.Services.AddSingleton<StartupState>();

        builder.Services.AddHealthChecks()
            // Liveness: Always healthy if the app is running
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            // Startup check: Gates readiness until MarkReady() is called
            .AddCheck<StartupHealthCheck>("startup", tags: [StartTag, ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Maps health check endpoints for Kubernetes probes using standard ASP.NET Core health checks:
    /// - /health/live - Liveness probe (is the app responsive?)
    /// - /health/start - Startup probe (has initialization completed?)
    /// - /health/ready - Readiness probe (can the app handle traffic?)
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="liveEndpoint">Liveness endpoint path (default: /health/live).</param>
    /// <param name="startEndpoint">Startup endpoint path (default: /health/start).</param>
    /// <param name="readyEndpoint">Readiness endpoint path (default: /health/ready).</param>
    /// <returns>The app for chaining.</returns>
    public static WebApplication MapHealthCheckEndpoints(
        this WebApplication app,
        string liveEndpoint = "/health/live",
        string startEndpoint = "/health/start",
        string readyEndpoint = "/health/ready")
    {
        // Liveness: Only checks tagged with "live"
        // Should NOT check external dependencies
        var liveBuilder = app.MapHealthChecks(liveEndpoint, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteResponse
        });

        // Startup: Only checks tagged with "start"
        // Gates startup until MarkAsReady() is called
        var startBuilder = app.MapHealthChecks(startEndpoint, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(StartTag),
            ResponseWriter = WriteResponse
        });

        // Readiness: Only checks tagged with "ready"
        // Includes database and startup state
        var readyBuilder = app.MapHealthChecks(readyEndpoint, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponse
        });

        // Standard ASP.NET Core health checks are excluded from OpenAPI by default
        liveBuilder.ExcludeFromDescription();
        startBuilder.ExcludeFromDescription();
        readyBuilder.ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Marks the application as ready and able to receive traffic.
    /// Call this after migrations and initialization complete.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The app for chaining.</returns>
    public static WebApplication MarkAsReady(this WebApplication app)
    {
        var startupState = app.Services.GetRequiredService<StartupState>();
        startupState.MarkReady();
        return app;
    }

    /// <summary>
    /// Gets the startup state service for manual ready state management.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The startup state instance.</returns>
    public static StartupState GetStartupState(this WebApplication app)
    {
        return app.Services.GetRequiredService<StartupState>();
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

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

        return context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// Response model for health check endpoints.
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Overall health status (Healthy, Degraded, or Unhealthy).
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Total duration of all health checks in milliseconds.
    /// </summary>
    public double TotalDuration { get; set; }

    /// <summary>
    /// Individual health check results.
    /// </summary>
    public List<HealthCheckEntry> Checks { get; set; } = new();
}

/// <summary>
/// Individual health check entry.
/// </summary>
public class HealthCheckEntry
{
    /// <summary>
    /// Name of the health check.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Status of this health check (Healthy, Degraded, or Unhealthy).
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Duration of this health check in milliseconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Optional description of the health check result.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Exception message if the health check failed.
    /// </summary>
    public string? Exception { get; set; }
}
