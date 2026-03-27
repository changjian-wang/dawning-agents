namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Represents the MCP server capabilities declaration.
/// </summary>
public sealed class MCPServerCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; set; }

    [JsonPropertyName("resources")]
    public ResourcesCapability? Resources { get; set; }

    [JsonPropertyName("prompts")]
    public PromptsCapability? Prompts { get; set; }

    [JsonPropertyName("logging")]
    public LoggingCapability? Logging { get; set; }
}

/// <summary>
/// Represents the tools capability.
/// </summary>
public sealed class ToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Represents the resources capability.
/// </summary>
public sealed class ResourcesCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; set; }

    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Represents the prompts capability.
/// </summary>
public sealed class PromptsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Represents the logging capability.
/// </summary>
public sealed class LoggingCapability { }

/// <summary>
/// Represents the MCP client capabilities declaration.
/// </summary>
public sealed class MCPClientCapabilities
{
    [JsonPropertyName("roots")]
    public RootsCapability? Roots { get; set; }

    [JsonPropertyName("sampling")]
    public SamplingCapability? Sampling { get; set; }
}

/// <summary>
/// Represents the roots capability.
/// </summary>
public sealed class RootsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// Represents the sampling capability.
/// </summary>
public sealed class SamplingCapability { }

/// <summary>
/// Represents MCP server information.
/// </summary>
public sealed class MCPServerInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// Represents MCP client information.
/// </summary>
public sealed class MCPClientInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// Represents the initialization request parameters.
/// </summary>
public sealed class InitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public required MCPClientCapabilities Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public required MCPClientInfo ClientInfo { get; set; }
}

/// <summary>
/// Represents the initialization response result.
/// </summary>
public sealed class InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public required string ProtocolVersion { get; set; }

    [JsonPropertyName("capabilities")]
    public required MCPServerCapabilities Capabilities { get; set; }

    [JsonPropertyName("serverInfo")]
    public required MCPServerInfo ServerInfo { get; set; }
}
