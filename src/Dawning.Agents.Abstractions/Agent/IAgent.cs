namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Core agent interface.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Agent name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent system instructions.
    /// </summary>
    string Instructions { get; }

    /// <summary>
    /// Executes an agent task.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Agent response.</returns>
    Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an agent task with the specified context.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Agent response.</returns>
    Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    );
}
