namespace Dawning.Agents.Abstractions.Orchestration;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Orchestration execution result.
/// </summary>
public record OrchestrationResult
{
    /// <summary>
    /// Whether execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Final output.
    /// </summary>
    public string? FinalOutput { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Execution result for each Agent.
    /// </summary>
    public IReadOnlyList<AgentExecutionRecord> AgentResults { get; init; } = [];

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static OrchestrationResult Successful(
        string finalOutput,
        IReadOnlyList<AgentExecutionRecord> agentResults,
        TimeSpan duration,
        IReadOnlyDictionary<string, object>? metadata = null
    ) =>
        new()
        {
            Success = true,
            FinalOutput = finalOutput,
            AgentResults = agentResults,
            Duration = duration,
            Metadata = metadata,
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static OrchestrationResult Failed(
        string error,
        IReadOnlyList<AgentExecutionRecord> agentResults,
        TimeSpan duration
    ) =>
        new()
        {
            Success = false,
            Error = error,
            AgentResults = agentResults,
            Duration = duration,
        };
}

/// <summary>
/// Execution record for a single Agent.
/// </summary>
public record AgentExecutionRecord
{
    /// <summary>
    /// Agent name.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Input content.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Agent response.
    /// </summary>
    public required AgentResponse Response { get; init; }

    /// <summary>
    /// Execution order (starting from 0).
    /// </summary>
    public int ExecutionOrder { get; init; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTimeOffset EndTime { get; init; }
}
