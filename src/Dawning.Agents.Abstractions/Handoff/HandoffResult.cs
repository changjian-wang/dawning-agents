using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff result.
/// </summary>
public record HandoffResult
{
    /// <summary>
    /// Whether the handoff completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Name of the Agent that executed the task.
    /// </summary>
    public required string ExecutedByAgent { get; init; }

    /// <summary>
    /// Agent response.
    /// </summary>
    public AgentResponse? Response { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Handoff chain record (path from source Agent to final Agent).
    /// </summary>
    public IReadOnlyList<HandoffRecord> HandoffChain { get; init; } = [];

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static HandoffResult Successful(
        string executedBy,
        AgentResponse response,
        IReadOnlyList<HandoffRecord> chain,
        TimeSpan duration
    ) =>
        new()
        {
            Success = true,
            ExecutedByAgent = executedBy,
            Response = response,
            HandoffChain = chain,
            TotalDuration = duration,
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static HandoffResult Failed(
        string executedBy,
        string error,
        IReadOnlyList<HandoffRecord> chain,
        TimeSpan duration
    ) =>
        new()
        {
            Success = false,
            ExecutedByAgent = executedBy,
            Error = error,
            HandoffChain = chain,
            TotalDuration = duration,
        };
}

/// <summary>
/// Handoff record - details of each transfer.
/// </summary>
public record HandoffRecord
{
    /// <summary>
    /// Source Agent name (null indicates the initial request).
    /// </summary>
    public string? FromAgent { get; init; }

    /// <summary>
    /// Target Agent name.
    /// </summary>
    public required string ToAgent { get; init; }

    /// <summary>
    /// Handoff reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Handoff timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Passed input.
    /// </summary>
    public required string Input { get; init; }
}
