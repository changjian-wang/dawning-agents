namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent single-step execution record.
/// </summary>
public record AgentStep
{
    /// <summary>
    /// Step number.
    /// </summary>
    public required int StepNumber { get; init; }

    /// <summary>
    /// Raw LLM output (used for debugging and final answer extraction).
    /// </summary>
    public string? RawOutput { get; init; }

    /// <summary>
    /// Agent's reasoning process.
    /// </summary>
    public string? Thought { get; init; }

    /// <summary>
    /// Name of the executed action.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Input parameters for the action.
    /// </summary>
    public string? ActionInput { get; init; }

    /// <summary>
    /// Observation result after action execution.
    /// </summary>
    public string? Observation { get; init; }

    /// <summary>
    /// Step execution timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Cost of this step (USD).
    /// </summary>
    public decimal Cost { get; init; }
}
