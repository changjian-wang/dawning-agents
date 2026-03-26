namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent state checkpoint — supports saving and restoring agent execution context.
/// </summary>
public interface IAgentCheckpoint
{
    /// <summary>
    /// Saves an agent context snapshot.
    /// </summary>
    /// <param name="sessionId">Session ID (used as the checkpoint key).</param>
    /// <param name="context">Agent context to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        string sessionId,
        AgentContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Loads an agent context snapshot.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved context, or <c>null</c> if not found.</returns>
    Task<AgentContext?> LoadAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a checkpoint.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a checkpoint exists.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
}
