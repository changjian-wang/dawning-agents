using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Scaling;

/// <summary>
/// Configuration options for agent scaling.
/// </summary>
/// <remarks>
/// Example appsettings.json:
/// <code>
/// {
///   "Scaling": {
///     "MinInstances": 1,
///     "MaxInstances": 10,
///     "TargetCpuPercent": 70,
///     "TargetMemoryPercent": 80
///   }
/// }
/// </code>
/// </remarks>
public record ScalingOptions : IValidatableOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Scaling";

    /// <summary>
    /// Gets the minimum number of instances.
    /// </summary>
    public int MinInstances { get; init; } = 1;

    /// <summary>
    /// Gets the maximum number of instances.
    /// </summary>
    public int MaxInstances { get; init; } = 10;

    /// <summary>
    /// Gets the target CPU usage percentage.
    /// </summary>
    public int TargetCpuPercent { get; init; } = 70;

    /// <summary>
    /// Gets the target memory usage percentage.
    /// </summary>
    public int TargetMemoryPercent { get; init; } = 80;

    /// <summary>
    /// Gets the scale-up cooldown period in seconds.
    /// </summary>
    public int ScaleUpCooldownSeconds { get; init; } = 60;

    /// <summary>
    /// Gets the scale-down cooldown period in seconds.
    /// </summary>
    public int ScaleDownCooldownSeconds { get; init; } = 300;

    /// <summary>
    /// Gets the work queue capacity.
    /// </summary>
    public int QueueCapacity { get; init; } = 1000;

    /// <summary>
    /// Gets the number of worker threads (0 = auto-detect).
    /// </summary>
    public int WorkerCount { get; init; } = 0;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (MinInstances < 1)
        {
            throw new InvalidOperationException("MinInstances must be at least 1");
        }

        if (MaxInstances < MinInstances)
        {
            throw new InvalidOperationException("MaxInstances must be >= MinInstances");
        }

        if (TargetCpuPercent < 1 || TargetCpuPercent > 100)
        {
            throw new InvalidOperationException("TargetCpuPercent must be between 1 and 100");
        }

        if (TargetMemoryPercent < 1 || TargetMemoryPercent > 100)
        {
            throw new InvalidOperationException("TargetMemoryPercent must be between 1 and 100");
        }

        if (ScaleUpCooldownSeconds < 0)
        {
            throw new InvalidOperationException("ScaleUpCooldownSeconds must be non-negative");
        }

        if (ScaleDownCooldownSeconds < 0)
        {
            throw new InvalidOperationException("ScaleDownCooldownSeconds must be non-negative");
        }

        if (QueueCapacity < 1)
        {
            throw new InvalidOperationException("QueueCapacity must be at least 1");
        }

        if (WorkerCount < 0)
        {
            throw new InvalidOperationException("WorkerCount must be non-negative");
        }
    }

    /// <summary>
    /// Gets the actual worker thread count, using auto-detection if configured.
    /// </summary>
    public int GetActualWorkerCount() =>
        WorkerCount > 0 ? WorkerCount : Environment.ProcessorCount * 2;
}

/// <summary>
/// Represents a point-in-time snapshot of scaling metrics.
/// </summary>
public record ScalingMetrics
{
    /// <summary>
    /// Gets the CPU usage percentage.
    /// </summary>
    public double CpuPercent { get; init; }

    /// <summary>
    /// Gets the memory usage percentage.
    /// </summary>
    public double MemoryPercent { get; init; }

    /// <summary>
    /// Gets the queue length.
    /// </summary>
    public int QueueLength { get; init; }

    /// <summary>
    /// Gets the number of active requests.
    /// </summary>
    public int ActiveRequests { get; init; }

    /// <summary>
    /// Gets the average latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>
    /// Gets the timestamp when the metrics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an auto-scaling decision.
/// </summary>
public record ScalingDecision
{
    /// <summary>
    /// Gets the scaling action.
    /// </summary>
    public ScalingAction Action { get; init; }

    /// <summary>
    /// Gets the instance count delta.
    /// </summary>
    public int Delta { get; init; }

    /// <summary>
    /// Gets the reason for the decision.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the timestamp of the decision.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a no-op decision.
    /// </summary>
    public static ScalingDecision None => new() { Action = ScalingAction.None };

    /// <summary>
    /// Creates a scale-up decision.
    /// </summary>
    public static ScalingDecision ScaleUp(int delta, string reason) =>
        new()
        {
            Action = ScalingAction.ScaleUp,
            Delta = delta,
            Reason = reason,
        };

    /// <summary>
    /// Creates a scale-down decision.
    /// </summary>
    public static ScalingDecision ScaleDown(int delta, string reason) =>
        new()
        {
            Action = ScalingAction.ScaleDown,
            Delta = delta,
            Reason = reason,
        };
}

/// <summary>
/// Defines scaling action types.
/// </summary>
public enum ScalingAction
{
    /// <summary>
    /// No action.
    /// </summary>
    None,

    /// <summary>
    /// Scale up (add instances).
    /// </summary>
    ScaleUp,

    /// <summary>
    /// Scale down (remove instances).
    /// </summary>
    ScaleDown,
}

/// <summary>
/// Defines circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Closed (normal operation).
    /// </summary>
    Closed,

    /// <summary>
    /// Open (blocking requests).
    /// </summary>
    Open,

    /// <summary>
    /// Half-open (testing recovery).
    /// </summary>
    HalfOpen,
}
