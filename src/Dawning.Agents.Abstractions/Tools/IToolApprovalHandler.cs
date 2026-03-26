namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool approval handler interface — handles execution confirmation for high-risk tools.
/// </summary>
/// <remarks>
/// <para>For tools that require confirmation (RequiresConfirmation = true), approval is needed before execution.</para>
/// <para>Different implementations can support various approval strategies: automatic, interactive, risk-level-based, etc.</para>
/// </remarks>
public interface IToolApprovalHandler
{
    /// <summary>
    /// Requests approval for tool execution.
    /// </summary>
    /// <param name="tool">The tool to execute.</param>
    /// <param name="input">Tool input parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether execution is approved.</returns>
    Task<bool> RequestApprovalAsync(
        ITool tool,
        string input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Requests approval for URL access (for network request tools).
    /// </summary>
    /// <param name="tool">The tool initiating the request.</param>
    /// <param name="url">The URL to access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether access is approved.</returns>
    Task<bool> RequestUrlApprovalAsync(
        ITool tool,
        string url,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Requests approval for terminal command execution.
    /// </summary>
    /// <param name="tool">The tool initiating the request.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether execution is approved.</returns>
    Task<bool> RequestCommandApprovalAsync(
        ITool tool,
        string command,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Approval strategy enumeration.
/// </summary>
public enum ApprovalStrategy
{
    /// <summary>
    /// Always approve automatically.
    /// </summary>
    AlwaysApprove,

    /// <summary>
    /// Always deny (read-only mode).
    /// </summary>
    AlwaysDeny,

    /// <summary>
    /// Decide based on risk level (Low: auto-approve, Medium/High: require confirmation).
    /// </summary>
    RiskBased,

    /// <summary>
    /// Always require interactive confirmation.
    /// </summary>
    Interactive,
}
