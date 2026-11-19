using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ServiceDefaults.HealthChecks;

/// <summary>
/// Restricts an endpoint to the management port by the connection's local port; any other port answers 404.
/// </summary>
public static class ManagementPortEndpointExtensions
{
    /// <summary>Answers the endpoint only on <paramref name="managementPort"/>, 404 on every other local port.</summary>
    public static TBuilder RequireManagementPort<TBuilder>(this TBuilder endpoint, int managementPort)
        where TBuilder : IEndpointConventionBuilder
    {
        endpoint.Add(builder =>
        {
            var next = builder.RequestDelegate
                ?? throw new InvalidOperationException("A management endpoint has no request delegate.");
            builder.RequestDelegate = context => context.Connection.LocalPort == managementPort
                ? next(context)
                : RespondNotFound(context);
        });
        return endpoint;
    }

    private static Task RespondNotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
