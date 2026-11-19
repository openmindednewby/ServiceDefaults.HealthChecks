using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace ServiceDefaults.HealthChecks.Tests;

public sealed class HealthCheckManagementPortTests
{
    private const int PublicPort = 8080;
    private const int ManagementPort = 8081;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/start")]
    [InlineData("/health/ready")]
    public async Task MapHealthCheckEndpoints_WithManagementPortOnPublicPort_Returns404(string path)
    {
        await using var app = await StartAsync(new(), enableManagementPort: true);

        var status = await GetAsync(app, PublicPort, path);

        status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/start")]
    [InlineData("/health/ready")]
    public async Task MapHealthCheckEndpoints_WithManagementPortOnManagementPort_Returns200(string path)
    {
        await using var app = await StartAsync(new(), enableManagementPort: true);

        var status = await GetAsync(app, ManagementPort, path);

        status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapHealthCheckEndpoints_WithManagementPortSwitchedOnByConfiguration_Returns404OnPublicPort()
    {
        await using var app = await StartAsync(new() { ["HealthChecks:EnableManagementPort"] = "true" }, enableManagementPort: false);

        var status = await GetAsync(app, PublicPort, "/health/live");

        status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/start")]
    [InlineData("/health/ready")]
    public async Task MapHealthCheckEndpoints_WithoutManagementPort_Returns200OnPublicPort(string path)
    {
        await using var app = await StartAsync(new(), enableManagementPort: false);

        var status = await GetAsync(app, PublicPort, path);

        status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapHealthCheckEndpoints_WithSpoofedManagementHostOnPublicPort_Returns404()
    {
        await using var app = await StartAsync(new(), enableManagementPort: true);

        var status = await GetAsync(app, PublicPort, "/health/live", host: $"localhost:{ManagementPort}");

        status.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<HttpStatusCode> GetAsync(WebApplication app, int localPort, string path, string host = "localhost")
    {
        var context = await app.GetTestServer().SendAsync(request =>
        {
            request.Connection.LocalPort = localPort;
            request.Request.Method = HttpMethods.Get;
            request.Request.Path = path;
            request.Request.Host = new HostString(host);
        });
        return (HttpStatusCode)context.Response.StatusCode;
    }

    private static async Task<WebApplication> StartAsync(Dictionary<string, string?> settings, bool enableManagementPort)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        if (enableManagementPort)
            builder.AddHealthCheckDefaults(o => o.EnableManagementPort = true);
        else
            builder.AddHealthCheckDefaults();

        var app = builder.Build();
        app.MapHealthCheckEndpoints();
        app.MarkAsReady();
        await app.StartAsync();
        return app;
    }
}
