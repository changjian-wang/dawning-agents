namespace Dawning.Agents.Abstractions.Diagnostics;

/// <summary>
/// Performance profiler interface.
/// </summary>
public interface IPerformanceProfiler
{
    /// <summary>
    /// Starts timing an operation.
    /// </summary>
    /// <param name="operationName">Operation name.</param>
    /// <param name="category">Operation category.</param>
    /// <returns>A timing handle that stops timing when disposed.</returns>
    IDisposable StartOperation(string operationName, string? category = null);

    /// <summary>
    /// Records a completed operation.
    /// </summary>
    /// <param name="operationName">Operation name.</param>
    /// <param name="duration">Duration.</param>
    /// <param name="category">Operation category.</param>
    /// <param name="metadata">Additional metadata.</param>
    void RecordOperation(
        string operationName,
        TimeSpan duration,
        string? category = null,
        IReadOnlyDictionary<string, object>? metadata = null
    );

    /// <summary>
    /// Gets a list of slow operations.
    /// </summary>
    /// <param name="threshold">Slow operation threshold.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    IReadOnlyList<OperationTrace> GetSlowOperations(TimeSpan threshold, int limit = 100);

    /// <summary>
    /// Gets operation statistics.
    /// </summary>
    /// <param name="category">Operation category (optional).</param>
    IReadOnlyDictionary<string, OperationStatistics> GetStatistics(string? category = null);

    /// <summary>
    /// Clears history.
    /// </summary>
    void Clear();
}

/// <summary>
/// Operation trace record.
/// </summary>
public class OperationTrace
{
    /// <summary>
    /// Operation name.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Operation category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Duration (milliseconds).
    /// </summary>
    public double DurationMs => Duration.TotalMilliseconds;

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Operation statistics.
/// </summary>
public class OperationStatistics
{
    /// <summary>
    /// Operation name.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Total invocation count.
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// Success count.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Failure count.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Success rate.
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

    /// <summary>
    /// Total duration.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Average duration.
    /// </summary>
    public TimeSpan AverageDuration =>
        TotalCount > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalCount) : TimeSpan.Zero;

    /// <summary>
    /// Minimum duration.
    /// </summary>
    public TimeSpan MinDuration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Maximum duration.
    /// </summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Last invocation time.
    /// </summary>
    public DateTimeOffset LastCallTime { get; set; }
}

/// <summary>
/// Predefined operation categories.
/// </summary>
public static class OperationCategories
{
    /// <summary>
    /// LLM calls.
    /// </summary>
    public const string LLM = "LLM";

    /// <summary>
    /// Tool execution.
    /// </summary>
    public const string Tool = "Tool";

    /// <summary>
    /// Agent execution.
    /// </summary>
    public const string Agent = "Agent";

    /// <summary>
    /// RAG retrieval.
    /// </summary>
    public const string RAG = "RAG";

    /// <summary>
    /// Database operations.
    /// </summary>
    public const string Database = "Database";

    /// <summary>
    /// HTTP requests.
    /// </summary>
    public const string Http = "Http";

    /// <summary>
    /// Cache operations.
    /// </summary>
    public const string Cache = "Cache";
}
