namespace Dawning.Agents.MCP.Server;

using Dawning.Agents.MCP.Protocol;

/// <summary>
/// Defines a provider for MCP resources.
/// </summary>
public interface IMCPResourceProvider
{
    /// <summary>
    /// Gets all available resources.
    /// </summary>
    IEnumerable<MCPResource> GetResources();

    /// <summary>
    /// Gets resource templates.
    /// </summary>
    IEnumerable<MCPResourceTemplate> GetResourceTemplates() => [];

    /// <summary>
    /// Reads the content of a resource.
    /// </summary>
    Task<ResourceContent?> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Determines whether the specified URI is supported.
    /// </summary>
    bool SupportsUri(string uri);
}

/// <summary>
/// Defines a provider for MCP prompts.
/// </summary>
public interface IMCPPromptProvider
{
    /// <summary>
    /// Gets all available prompts.
    /// </summary>
    IEnumerable<MCPPrompt> GetPrompts();

    /// <summary>
    /// Gets the content of a prompt.
    /// </summary>
    Task<GetPromptResult?> GetPromptAsync(
        string name,
        Dictionary<string, string>? arguments,
        CancellationToken cancellationToken = default
    );
}
