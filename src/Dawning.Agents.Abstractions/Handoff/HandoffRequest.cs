namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff request - created when an Agent needs to transfer a task to another Agent.
/// </summary>
/// <remarks>
/// Handoff is the core mechanism for multi-Agent collaboration, enabling:
/// - Triage Agent routing user requests to specialist Agents
/// - Agents proactively transferring tasks when other expertise is needed
/// - Chained invocation of multiple Agents for complex tasks
/// </remarks>
public record HandoffRequest
{
    /// <summary>
    /// Target Agent name.
    /// </summary>
    public required string TargetAgentName { get; init; }

    /// <summary>
    /// Handoff reason/description.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Input to pass to the target Agent.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Additional context data.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Whether to preserve conversation history.
    /// </summary>
    public bool PreserveHistory { get; init; } = true;

    /// <summary>
    /// Creates a handoff request.
    /// </summary>
    public static HandoffRequest To(string targetAgent, string input, string? reason = null) =>
        new()
        {
            TargetAgentName = targetAgent,
            Input = input,
            Reason = reason,
        };
}
