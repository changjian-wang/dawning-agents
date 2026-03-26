namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Represents a message in a conversation.
/// </summary>
/// <param name="Role">Message role (user, assistant, system, tool).</param>
/// <param name="Content">Message content.</param>
public record ChatMessage(string Role, string Content)
{
    /// <summary>
    /// Sender name (optional; used to distinguish message sources in multi-agent scenarios).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Tool calls returned by the LLM (assistant role only).
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// Tool call ID (tool role only; correlates with the assistant's ToolCall).
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Whether the message contains tool calls.
    /// </summary>
    public bool HasToolCalls => ToolCalls is { Count: > 0 };

    /// <summary>Creates a user message.</summary>
    public static ChatMessage User(string content) => new("user", content);

    /// <summary>Creates an assistant message.</summary>
    public static ChatMessage Assistant(string content) => new("assistant", content);

    /// <summary>Creates a system message.</summary>
    public static ChatMessage System(string content) => new("system", content);

    /// <summary>Creates an assistant message with tool calls.</summary>
    public static ChatMessage AssistantWithToolCalls(
        IReadOnlyList<ToolCall> toolCalls,
        string content = ""
    ) => new("assistant", content) { ToolCalls = toolCalls };

    /// <summary>Creates a tool result message.</summary>
    public static ChatMessage ToolResult(string toolCallId, string content) =>
        new("tool", content) { ToolCallId = toolCallId };
}
