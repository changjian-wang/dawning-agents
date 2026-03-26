namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Read-only tool query interface — retrieves registered tools.
/// </summary>
public interface IToolReader
{
    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="name">Tool name (case-insensitive).</param>
    /// <returns>The tool instance, or <see langword="null"/> if not found.</returns>
    ITool? GetTool(string name);

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    IReadOnlyList<ITool> GetAllTools();

    /// <summary>
    /// Checks whether a tool is registered.
    /// </summary>
    /// <param name="name">Tool name.</param>
    bool HasTool(string name);

    /// <summary>
    /// Number of registered tools.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets tools by category.
    /// </summary>
    /// <param name="category">Tool category.</param>
    /// <returns>All tools belonging to the specified category.</returns>
    IReadOnlyList<ITool> GetToolsByCategory(string category);

    /// <summary>
    /// Gets all tool categories.
    /// </summary>
    /// <returns>List of category names.</returns>
    IReadOnlyList<string> GetCategories();
}

/// <summary>
/// Tool registrar interface — registers tools into the registry.
/// </summary>
public interface IToolRegistrar
{
    /// <summary>
    /// Registers a tool.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    void Register(ITool tool);
}

/// <summary>
/// Tool registry — manages and retrieves registered tools (composite interface for backward compatibility).
/// </summary>
public interface IToolRegistry : IToolReader, IToolRegistrar { }
