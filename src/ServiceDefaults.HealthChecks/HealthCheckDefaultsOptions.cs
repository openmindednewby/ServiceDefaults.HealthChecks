namespace ServiceDefaults.HealthChecks;

/// <summary>
/// Settings for the health check endpoints, bound from the <c>HealthChecks</c> configuration section.
/// </summary>
public sealed class HealthCheckDefaultsOptions
{
    public const string SectionName = "HealthChecks";

    public const int DefaultManagementPort = 8081;

    public bool EnableManagementPort { get; set; }

    public int ManagementPort { get; set; } = DefaultManagementPort;
}
