namespace ServiceDefaults.HealthChecks;

/// <summary>
/// Tracks application startup state for health check readiness.
/// Use this to gate readiness until migrations and initialization complete.
/// </summary>
public sealed class StartupState
{
    private volatile bool _ready;

    /// <summary>
    /// Gets whether the application has completed startup initialization.
    /// </summary>
    public bool IsReady => _ready;

    /// <summary>
    /// Marks the application as ready to receive traffic.
    /// Call this after migrations and initialization are complete.
    /// </summary>
    public void MarkReady() => _ready = true;

    /// <summary>
    /// Marks the application as not ready (e.g., for graceful shutdown).
    /// </summary>
    public void MarkNotReady() => _ready = false;
}
