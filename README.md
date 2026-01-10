# ServiceDefaults.HealthChecks

Production-ready health check infrastructure for ASP.NET Core services with **separate liveness, readiness, and startup** endpoints - designed for Kubernetes deployments.

## Installation

```bash
dotnet add package ServiceDefaults.HealthChecks
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add health check infrastructure
builder.AddHealthCheckDefaults();

// Add PostgreSQL readiness check
builder.AddPostgresReadinessCheck(
    builder.Configuration.GetConnectionString("Postgres")!);

var app = builder.Build();

// Run migrations BEFORE marking ready
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Mark app as ready AFTER migrations complete
app.MarkAsReady();

// Map health endpoints
app.MapHealthCheckEndpoints();

app.Run();
```

## Health Check Endpoints

| Endpoint | Purpose | Checks | Kubernetes Probe |
|----------|---------|--------|------------------|
| `/health/live` | Is the app running? | Self check only | `livenessProbe` |
| `/health/start` | Has startup finished? | Startup state only | `startupProbe` |
| `/health/ready` | Can it handle traffic? | DB + Startup state | `readinessProbe` |

## Why Separate Endpoints?

| Probe | Checks | Fails when | Effect |
|-------|--------|------------|--------|
| **Liveness** | App loop only | App is wedged | Container restarted |
| **Readiness** | DB + migrations | Traffic would fail | Removed from LB |

**Critical Rule**: Liveness should **NEVER** check external dependencies like databases. A database hiccup shouldn't restart your containers.

## Kubernetes Configuration

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  periodSeconds: 10
  timeoutSeconds: 1
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  periodSeconds: 5
  timeoutSeconds: 2
  failureThreshold: 1

startupProbe:
  httpGet:
    path: /health/start
    port: 8080
  failureThreshold: 30
  periodSeconds: 5
```

## API Reference

### Builder Extensions

```csharp
// Add core health checks (liveness + startup state)
builder.AddHealthCheckDefaults();

// Add PostgreSQL readiness check
builder.AddPostgresReadinessCheck(connectionString, name: "postgres", timeout: TimeSpan.FromSeconds(2));
```

### App Extensions

```csharp
// Map endpoints (defaults: /health/live, /health/ready)
app.MapHealthCheckEndpoints();

// Custom paths
app.MapHealthCheckEndpoints(
    liveEndpoint: "/healthz",
    readyEndpoint: "/ready");

// Mark ready after initialization
app.MarkAsReady();

// Get startup state for manual control
var state = app.GetStartupState();
state.MarkReady();
state.MarkNotReady(); // For graceful shutdown
```

### StartupState

The `StartupState` class tracks whether your app has completed initialization:

```csharp
// Injected via DI
public class MyService
{
    private readonly StartupState _startupState;

    public MyService(StartupState startupState)
    {
        _startupState = startupState;
    }

    public bool IsReady => _startupState.IsReady;
}
```

## Response Format

Health endpoints return JSON:

```json
{
  "status": "Healthy",
  "totalDuration": 12.5,
  "checks": [
    {
      "name": "postgres",
      "status": "Healthy",
      "duration": 10.2,
      "description": null,
      "exception": null
    },
    {
      "name": "startup",
      "status": "Healthy",
      "duration": 0.1,
      "description": "Application startup complete",
      "exception": null
    }
  ]
}
```

## License

MIT License
