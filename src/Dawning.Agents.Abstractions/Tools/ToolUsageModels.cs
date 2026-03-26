namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Single tool execution record.
/// </summary>
public record ToolUsageRecord
{
    /// <summary>
    /// Tool name.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Task context description.
    /// </summary>
    public string? TaskContext { get; init; }

    /// <summary>
    /// Record timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tool utility statistics.
/// </summary>
public record ToolUsageStats
{
    /// <summary>
    /// Tool name.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Total number of calls.
    /// </summary>
    public int TotalCalls { get; init; }

    /// <summary>
    /// Success count.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Failure count.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Success rate (0–1).
    /// </summary>
    public float SuccessRate => TotalCalls > 0 ? (float)SuccessCount / TotalCalls : 0f;

    /// <summary>
    /// Average latency.
    /// </summary>
    public TimeSpan AverageLatency { get; init; }

    /// <summary>
    /// Last used time.
    /// </summary>
    public DateTimeOffset LastUsed { get; init; }

    /// <summary>
    /// Recent error messages (retains the most recent N entries).
    /// </summary>
    public IReadOnlyList<string> RecentErrors { get; init; } = [];
}
