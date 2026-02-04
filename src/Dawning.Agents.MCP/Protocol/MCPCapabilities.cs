namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// MCP 服务器能力声明
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
/// 工具能力
/// </summary>
public sealed class ToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// 资源能力
/// </summary>
public sealed class ResourcesCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; set; }

    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// 提示词能力
/// </summary>
public sealed class PromptsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// 日志能力
/// </summary>
public sealed class LoggingCapability { }

/// <summary>
/// MCP 客户端能力声明
/// </summary>
public sealed class MCPClientCapabilities
{
    [JsonPropertyName("roots")]
    public RootsCapability? Roots { get; set; }

    [JsonPropertyName("sampling")]
    public SamplingCapability? Sampling { get; set; }
}

/// <summary>
/// 根目录能力
/// </summary>
public sealed class RootsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// 采样能力
/// </summary>
public sealed class SamplingCapability { }

/// <summary>
/// MCP 服务器信息
/// </summary>
public sealed class MCPServerInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// MCP 客户端信息
/// </summary>
public sealed class MCPClientInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// 初始化请求参数
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
/// 初始化响应结果
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
