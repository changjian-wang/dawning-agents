namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent execution response.
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Final answer.
    /// </summary>
    public string? FinalAnswer { get; init; }

    /// <summary>
    /// Error message, if the execution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// All execution steps.
    /// </summary>
    public IReadOnlyList<AgentStep> Steps { get; init; } = [];

    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Total cost (USD).
    /// </summary>
    public decimal TotalCost { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static AgentResponse Successful(
        string finalAnswer,
        IReadOnlyList<AgentStep> steps,
        TimeSpan duration
    ) =>
        new()
        {
            Success = true,
            FinalAnswer = finalAnswer,
            Steps = steps,
            Duration = duration,
            TotalCost = steps.Sum(s => s.Cost),
        };

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static AgentResponse Failed(
        string error,
        IReadOnlyList<AgentStep> steps,
        TimeSpan duration,
        Exception? exception = null
    ) =>
        new()
        {
            Success = false,
            Error = error,
            Steps = steps,
            Duration = duration,
            TotalCost = steps.Sum(s => s.Cost),
            Exception = exception,
        };
}
