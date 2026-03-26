using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff Agent interface - an Agent with handoff capabilities.
/// </summary>
/// <remarks>
/// Extends IAgent with handoff-related capabilities:
/// - Declares a list of target Agents that can receive handoffs
/// - Can request handoffs during execution
/// </remarks>
public interface IHandoffAgent : IAgent
{
    /// <summary>
    /// List of target Agent names this Agent can hand off to.
    /// </summary>
    /// <remarks>
    /// If empty, this Agent will not initiate handoffs.
    /// </remarks>
    IReadOnlyList<string> Handoffs { get; }
}

/// <summary>
/// Handoff handler interface - manages and executes handoffs.
/// </summary>
public interface IHandoffHandler
{
    /// <summary>
    /// Registers an Agent with the handoff system.
    /// </summary>
    /// <param name="agent">The Agent to register.</param>
    void RegisterAgent(IAgent agent);

    /// <summary>
    /// Registers Agents in batch.
    /// </summary>
    /// <param name="agents">Collection of Agents to register.</param>
    void RegisterAgents(IEnumerable<IAgent> agents);

    /// <summary>
    /// Gets a registered Agent.
    /// </summary>
    /// <param name="name">Agent name.</param>
    /// <returns>Agent instance, or null if not found.</returns>
    IAgent? GetAgent(string name);

    /// <summary>
    /// Gets all registered Agents.
    /// </summary>
    IReadOnlyList<IAgent> GetAllAgents();

    /// <summary>
    /// Executes a handoff request.
    /// </summary>
    /// <param name="request">Handoff request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Handoff execution result.</returns>
    Task<HandoffResult> ExecuteHandoffAsync(
        HandoffRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Starts execution from the specified entry Agent with automatic handoff support.
    /// </summary>
    /// <param name="entryAgentName">Entry Agent name.</param>
    /// <param name="input">User input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final execution result.</returns>
    Task<HandoffResult> RunWithHandoffAsync(
        string entryAgentName,
        string input,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Agent response extensions - for detecting handoff requests.
/// </summary>
public static class AgentResponseHandoffExtensions
{
    /// <summary>
    /// Handoff marker prefix.
    /// </summary>
    public const string HandoffPrefix = "[HANDOFF:";

    /// <summary>
    /// Checks whether the response contains a handoff request.
    /// </summary>
    public static bool IsHandoffRequest(this AgentResponse response)
    {
        return response.Success
            && !string.IsNullOrEmpty(response.FinalAnswer)
            && response.FinalAnswer.StartsWith(HandoffPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a handoff request from the response.
    /// </summary>
    /// <returns>Parsed handoff request, or null if the response is not a handoff.</returns>
    public static HandoffRequest? ParseHandoffRequest(this AgentResponse response)
    {
        if (!response.IsHandoffRequest())
        {
            return null;
        }

        var answer = response.FinalAnswer!;

        // Format: [HANDOFF:TargetAgent] Input text
        // Or: [HANDOFF:TargetAgent|Reason] Input text
        var endBracket = answer.IndexOf(']');
        if (endBracket < 0)
        {
            return null;
        }

        var handoffPart = answer[HandoffPrefix.Length..endBracket];
        var input = answer[(endBracket + 1)..].Trim();

        string targetAgent;
        string? reason = null;

        var pipeIndex = handoffPart.IndexOf('|');
        if (pipeIndex > 0)
        {
            targetAgent = handoffPart[..pipeIndex].Trim();
            reason = handoffPart[(pipeIndex + 1)..].Trim();
        }
        else
        {
            targetAgent = handoffPart.Trim();
        }

        if (string.IsNullOrEmpty(targetAgent))
        {
            return null;
        }

        return new HandoffRequest
        {
            TargetAgentName = targetAgent,
            Input = string.IsNullOrEmpty(input) ? response.FinalAnswer! : input,
            Reason = reason,
        };
    }

    /// <summary>
    /// Creates a handoff response.
    /// </summary>
    /// <param name="targetAgent">Target Agent name.</param>
    /// <param name="input">Input to pass to the target Agent.</param>
    /// <param name="reason">Handoff reason.</param>
    /// <returns>Formatted handoff response string.</returns>
    public static string CreateHandoffResponse(
        string targetAgent,
        string input,
        string? reason = null
    )
    {
        if (string.IsNullOrEmpty(reason))
        {
            return $"{HandoffPrefix}{targetAgent}] {input}";
        }

        return $"{HandoffPrefix}{targetAgent}|{reason}] {input}";
    }
}
