namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Represents the result of a health check.
/// </summary>
public record HealthCheckResult
{
    /// <summary>
    /// Gets the overall health status.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// Gets the timestamp when the check was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the health status of each component.
    /// </summary>
    public IReadOnlyList<ComponentHealth> Components { get; init; } = [];
}

/// <summary>
/// Represents the health status of an individual component.
/// </summary>
public record ComponentHealth
{
    /// <summary>
    /// Gets the component name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the health status.
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// Gets an optional descriptive message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets optional additional data associated with the health check.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Defines health status values.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Degraded.
    /// </summary>
    Degraded,

    /// <summary>
    /// Unhealthy.
    /// </summary>
    Unhealthy,
}

/// <summary>
/// Provides health check capabilities for a component.
/// </summary>
public interface IHealthCheckProvider
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks the health status of the component.
    /// </summary>
    Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}
