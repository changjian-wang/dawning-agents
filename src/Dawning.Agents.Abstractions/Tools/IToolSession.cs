namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool session — manages session-scoped dynamic tools and aggregates multi-level tool resolution.
/// </summary>
/// <remarks>
/// <para>Tool resolution order: Core → Session → User → Global → MCP.</para>
/// <para>Session tools are stored in memory and destroyed with the session.</para>
/// </remarks>
public interface IToolSession : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Creates and registers a dynamic script tool in the current session.
    /// </summary>
    /// <param name="definition">Tool definition.</param>
    /// <returns>The created tool instance.</returns>
    ITool CreateTool(EphemeralToolDefinition definition);

    /// <summary>
    /// Gets all dynamic tools in the current session.
    /// </summary>
    IReadOnlyList<ITool> GetSessionTools();

    /// <summary>
    /// Promotes a tool's persistence level (Session → User or Global).
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="targetScope">Target scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PromoteToolAsync(
        string name,
        ToolScope targetScope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Removes a tool from the specified scope.
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="scope">Tool scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists all tool definitions in the specified scope.
    /// </summary>
    /// <param name="scope">Tool scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<EphemeralToolDefinition>> ListToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates a session tool's definition (in-place revision, used for reflective repair).
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="definition">Revised tool definition.</param>
    /// <returns>The updated tool instance.</returns>
    ITool UpdateTool(string name, EphemeralToolDefinition definition);
}
