namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// MCP 提示词定义
/// </summary>
public sealed class MCPPrompt
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("arguments")]
    public List<MCPPromptArgument>? Arguments { get; set; }
}

/// <summary>
/// 提示词参数定义
/// </summary>
public sealed class MCPPromptArgument
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

/// <summary>
/// 提示词列表请求参数
/// </summary>
public sealed class ListPromptsParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// 提示词列表响应
/// </summary>
public sealed class ListPromptsResult
{
    [JsonPropertyName("prompts")]
    public required List<MCPPrompt> Prompts { get; init; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// 获取提示词请求参数
/// </summary>
public sealed class GetPromptParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, string>? Arguments { get; set; }
}

/// <summary>
/// 获取提示词响应
/// </summary>
public sealed class GetPromptResult
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("messages")]
    public required List<MCPPromptMessage> Messages { get; set; }
}

/// <summary>
/// 提示词消息
/// </summary>
public sealed class MCPPromptMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required MCPContent Content { get; set; }
}

/// <summary>
/// MCP 方法名称常量
/// </summary>
public static class MCPMethods
{
    // 生命周期
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    public const string Shutdown = "shutdown";

    // 工具
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
    public const string ToolsListChanged = "notifications/tools/list_changed";

    // 资源
    public const string ResourcesList = "resources/list";
    public const string ResourcesTemplatesList = "resources/templates/list";
    public const string ResourcesRead = "resources/read";
    public const string ResourcesSubscribe = "resources/subscribe";
    public const string ResourcesUnsubscribe = "resources/unsubscribe";
    public const string ResourcesUpdated = "notifications/resources/updated";
    public const string ResourcesListChanged = "notifications/resources/list_changed";

    // 提示词
    public const string PromptsList = "prompts/list";
    public const string PromptsGet = "prompts/get";
    public const string PromptsListChanged = "notifications/prompts/list_changed";

    // 日志
    public const string LoggingSetLevel = "logging/setLevel";
    public const string LoggingMessage = "notifications/message";

    // 采样
    public const string SamplingCreateMessage = "sampling/createMessage";

    // 根目录
    public const string RootsList = "roots/list";
    public const string RootsListChanged = "notifications/roots/list_changed";

    // 进度
    public const string ProgressStart = "notifications/progress";
    public const string ProgressCancel = "notifications/cancelled";
}

/// <summary>
/// MCP 协议版本
/// </summary>
public static class MCPProtocolVersion
{
    public const string V2024_11_05 = "2024-11-05";
    public const string Latest = V2024_11_05;
}
