namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Response from a chat completion request.
/// </summary>
public record ChatCompletionResponse
{
    /// <summary>Response content.</summary>
    public required string Content { get; init; }

    /// <summary>Number of input tokens.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Number of output tokens.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total number of tokens.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>Finish reason (stop, length, content_filter, tool_calls, etc.).</summary>
    public string? FinishReason { get; init; }

    /// <summary>Tool calls returned by the LLM (only when FinishReason is tool_calls).</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>Whether the response contains tool calls.</summary>
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}
