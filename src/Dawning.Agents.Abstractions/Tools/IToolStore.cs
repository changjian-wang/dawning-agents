namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool persistent store — manages User and Global level tool definitions.
/// </summary>
/// <remarks>
/// <para>User tools are stored in the ~/.dawning/tools/ directory.</para>
/// <para>Global tools are stored in the {project}/.dawning/tools/ directory.</para>
/// </remarks>
public interface IToolStore
{
    /// <summary>
    /// Loads all tool definitions for the specified scope.
    /// </summary>
    /// <param name="scope">Tool scope (User or Global).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tool definitions.</returns>
    Task<IReadOnlyList<EphemeralToolDefinition>> LoadToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Saves a tool definition to the specified scope.
    /// </summary>
    /// <param name="definition">Tool definition.</param>
    /// <param name="scope">Tool scope (User or Global).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes a tool definition from the specified scope.
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="scope">Tool scope (User or Global).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks whether a tool exists in the specified scope.
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="scope">Tool scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates a tool definition (automatically increments version and revision time).
    /// </summary>
    /// <param name="definition">Revised tool definition.</param>
    /// <param name="scope">Tool scope (User or Global).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );
}
