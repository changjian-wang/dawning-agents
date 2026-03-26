namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Streaming chat event (structured streaming output).
/// </summary>
/// <remarks>
/// Replaces the raw <c>IAsyncEnumerable&lt;string&gt;</c>, providing content deltas, tool call deltas,
/// finish reasons, and token usage as structured information.
/// </remarks>
public record StreamingChatEvent
{
    /// <summary>Incremental text content.</summary>
    public string? ContentDelta { get; init; }

    /// <summary>Tool call delta (incrementally accumulated tool call information during streaming).</summary>
    public ToolCallDelta? ToolCallDelta { get; init; }

    /// <summary>Finish reason (present only in the last event).</summary>
    public string? FinishReason { get; init; }

    /// <summary>Token usage (present only in the last event).</summary>
    public StreamingTokenUsage? Usage { get; init; }

    /// <summary>Creates a content delta event.</summary>
    public static StreamingChatEvent Content(string text) => new() { ContentDelta = text };

    /// <summary>Creates a tool call delta event.</summary>
    public static StreamingChatEvent ToolCall(ToolCallDelta delta) =>
        new() { ToolCallDelta = delta };

    /// <summary>Creates a completion event with a finish reason and optional usage.</summary>
    public static StreamingChatEvent Done(string finishReason, StreamingTokenUsage? usage = null) =>
        new() { FinishReason = finishReason, Usage = usage };
}

/// <summary>
/// Streaming tool call delta.
/// </summary>
/// <remarks>
/// In a streaming response, tool call information may arrive across multiple chunks.
/// The client must accumulate and assemble complete ToolCall objects by Index.
/// </remarks>
public record ToolCallDelta
{
    /// <summary>Index of the tool call in the list.</summary>
    public int Index { get; init; }

    /// <summary>Tool call ID (typically appears in the first delta).</summary>
    public string? Id { get; init; }

    /// <summary>Function name (typically appears in the first delta).</summary>
    public string? FunctionName { get; init; }

    /// <summary>Arguments delta (JSON fragment that must be accumulated and concatenated).</summary>
    public string? ArgumentsDelta { get; init; }
}

/// <summary>
/// Token usage statistics for a streaming response.
/// </summary>
public record StreamingTokenUsage
{
    /// <summary>Number of input tokens.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Number of output tokens.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total number of tokens.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
