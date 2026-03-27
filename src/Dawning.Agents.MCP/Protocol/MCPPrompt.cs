namespace Dawning.Agents.MCP.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an MCP prompt definition.
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
/// Represents a prompt argument definition.
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
/// Represents the parameters for a list prompts request.
/// </summary>
public sealed class ListPromptsParams
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Represents the result of a list prompts request.
/// </summary>
public sealed class ListPromptsResult
{
    [JsonPropertyName("prompts")]
    public required List<MCPPrompt> Prompts { get; init; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// Represents the parameters for a get prompt request.
/// </summary>
public sealed class GetPromptParams
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("arguments")]
    public Dictionary<string, string>? Arguments { get; set; }
}

/// <summary>
/// Represents the result of a get prompt request.
/// </summary>
public sealed class GetPromptResult
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("messages")]
    public required List<MCPPromptMessage> Messages { get; set; }
}

/// <summary>
/// Represents a prompt message.
/// </summary>
public sealed class MCPPromptMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required MCPContent Content { get; set; }
}

/// <summary>
/// Defines MCP method name constants.
/// </summary>
public static class MCPMethods
{
    // Lifecycle
    public const string Initialize = "initialize";
    public const string Initialized = "notifications/initialized";
    public const string Shutdown = "shutdown";

    // Tools
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
    public const string ToolsListChanged = "notifications/tools/list_changed";

    // Resources
    public const string ResourcesList = "resources/list";
    public const string ResourcesTemplatesList = "resources/templates/list";
    public const string ResourcesRead = "resources/read";
    public const string ResourcesSubscribe = "resources/subscribe";
    public const string ResourcesUnsubscribe = "resources/unsubscribe";
    public const string ResourcesUpdated = "notifications/resources/updated";
    public const string ResourcesListChanged = "notifications/resources/list_changed";

    // Prompts
    public const string PromptsList = "prompts/list";
    public const string PromptsGet = "prompts/get";
    public const string PromptsListChanged = "notifications/prompts/list_changed";

    // Logging
    public const string LoggingSetLevel = "logging/setLevel";
    public const string LoggingMessage = "notifications/message";

    // Sampling
    public const string SamplingCreateMessage = "sampling/createMessage";

    // Roots
    public const string RootsList = "roots/list";
    public const string RootsListChanged = "notifications/roots/list_changed";

    // Progress
    public const string ProgressStart = "notifications/progress";
    public const string ProgressCancel = "notifications/cancelled";
}

/// <summary>
/// Defines MCP protocol version constants.
/// </summary>
public static class MCPProtocolVersion
{
    public const string V2024_11_05 = "2024-11-05";
    public const string Latest = V2024_11_05;
}
