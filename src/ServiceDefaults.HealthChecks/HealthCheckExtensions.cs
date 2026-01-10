using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
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
    /// Adds core health check services with liveness check.
    /// Call <see cref="AddPostgresReadinessCheck"/> to add database readiness.
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
            .AddCheck<StartupHealthCheck>("startup", tags: [ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Adds PostgreSQL connectivity check to readiness probes.
    /// </summary>
    /// <typeparam name="TBuilder">The host builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="name">Health check name (default: "postgres").</param>
    /// <param name="timeout">Query timeout (default: 2 seconds).</param>
    /// <returns>The builder for chaining.</returns>
    public static TBuilder AddPostgresReadinessCheck<TBuilder>(
        this TBuilder builder,
        string connectionString,
        string name = "postgres",
        TimeSpan? timeout = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionString,
                name: name,
                timeout: timeout ?? TimeSpan.FromSeconds(2),
                tags: [ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Maps health check endpoints for Kubernetes probes:
    /// - /health/live - Liveness probe (is the app responsive?)
    /// - /health/ready - Readiness probe (can the app handle traffic?)
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="liveEndpoint">Liveness endpoint path (default: /health/live).</param>
    /// <param name="readyEndpoint">Readiness endpoint path (default: /health/ready).</param>
    /// <returns>The app for chaining.</returns>
    public static WebApplication MapHealthCheckEndpoints(
        this WebApplication app,
        string liveEndpoint = "/health/live",
        string readyEndpoint = "/health/ready")
    {
        // Liveness: Only checks tagged with "live"
        // Should NOT check external dependencies
        app.MapHealthChecks(liveEndpoint, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteResponse
        });

        // Readiness: Only checks tagged with "ready"
        // Includes database and startup state
        app.MapHealthChecks(readyEndpoint, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponse
        });

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

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
