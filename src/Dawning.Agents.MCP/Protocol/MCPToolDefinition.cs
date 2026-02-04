namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// MCP 工具定义
/// </summary>
public sealed class MCPToolDefinition
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public required MCPInputSchema InputSchema { get; set; }
}

/// <summary>
/// MCP 工具输入参数 Schema (JSON Schema)
/// </summary>
public sealed class MCPInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, MCPPropertySchema>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

/// <summary>
/// MCP 属性 Schema
/// </summary>
public sealed class MCPPropertySchema
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("default")]
    public object? Default { get; set; }
}

/// <summary>
/// 工具列表请求参数
/// </summary>
public sealed class ListToolsParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// 工具列表响应
/// </summary>
public sealed class ListToolsResult
{
    [JsonPropertyName("tools")]
    public required List<MCPToolDefinition> Tools { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}

/// <summary>
/// 工具调用请求参数
/// </summary>
public sealed class CallToolParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?>? Arguments { get; set; }
}

/// <summary>
/// 工具调用结果
/// </summary>
public sealed class CallToolResult
{
    [JsonPropertyName("content")]
    public required List<MCPContent> Content { get; set; }

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// MCP 内容块
/// </summary>
public sealed class MCPContent
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    public static MCPContent TextContent(string text) =>
        new() { Type = "text", Text = text };

    public static MCPContent ImageContent(string base64Data, string mimeType = "image/png") =>
        new() { Type = "image", Data = base64Data, MimeType = mimeType };

    public static MCPContent ResourceContent(string uri, string text, string? mimeType = null) =>
        new() { Type = "resource", Text = text, MimeType = mimeType };
}
