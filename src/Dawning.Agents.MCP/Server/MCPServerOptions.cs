using Dawning.Agents.Abstractions;

namespace Dawning.Agents.MCP.Server;

/// <summary>
/// Configuration options for the MCP Server.
/// </summary>
public sealed class MCPServerOptions : IValidatableOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "MCP";

    /// <summary>
    /// Gets or sets the server name.
    /// </summary>
    public string Name { get; set; } = "Dawning.Agents.MCP";

    /// <summary>
    /// Gets or sets the server version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a value indicating whether tools are enabled.
    /// </summary>
    public bool EnableTools { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether resources are enabled.
    /// </summary>
    public bool EnableResources { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether prompts are enabled.
    /// </summary>
    public bool EnablePrompts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether logging is enabled.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the tool execution timeout in seconds.
    /// </summary>
    public int ToolTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the transport type.
    /// </summary>
    public MCPTransportType TransportType { get; set; } = MCPTransportType.Stdio;

    /// <summary>
    /// Gets or sets the HTTP port (used only for HTTP transport).
    /// </summary>
    public int HttpPort { get; set; } = 8080;

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("MCP Server Name is required");
        }

        if (ToolTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("ToolTimeoutSeconds must be greater than 0");
        }

        if (MaxConcurrentRequests <= 0)
        {
            throw new InvalidOperationException("MaxConcurrentRequests must be greater than 0");
        }

        if (HttpPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("HttpPort must be between 1 and 65535");
        }
    }
}

/// <summary>
/// Specifies the MCP transport type.
/// </summary>
public enum MCPTransportType
{
    /// <summary>
    /// Standard input/output streams (recommended, compatible with Claude Desktop).
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP + Server-Sent Events
    /// </summary>
    Http,
}
