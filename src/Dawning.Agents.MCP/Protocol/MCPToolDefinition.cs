namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an MCP tool definition.
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
/// Represents the MCP tool input parameter schema (JSON Schema).
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
/// Represents an MCP property schema.
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
/// Represents the parameters for a list tools request.
/// </summary>
public sealed class ListToolsParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Represents the result of a list tools request.
/// </summary>
public sealed class ListToolsResult
{
    [JsonPropertyName("tools")]
    public required List<MCPToolDefinition> Tools { get; init; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// Represents the parameters for a tool call request.
/// </summary>
public sealed class CallToolParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?>? Arguments { get; set; }
}

/// <summary>
/// Represents a tool call result.
/// </summary>
public sealed class CallToolResult
{
    [JsonPropertyName("content")]
    public required List<MCPContent> Content { get; set; }

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// Represents an MCP content block.
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

    public static MCPContent TextContent(string text) => new() { Type = "text", Text = text };

    public static MCPContent ImageContent(string base64Data, string mimeType = "image/png") =>
        new()
        {
            Type = "image",
            Data = base64Data,
            MimeType = mimeType,
        };

    public static MCPContent ResourceContent(string uri, string text, string? mimeType = null) =>
        new()
        {
            Type = "resource",
            Text = text,
            MimeType = mimeType,
        };
}
