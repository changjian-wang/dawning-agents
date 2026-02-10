namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 聊天完成请求的响应
/// </summary>
public record ChatCompletionResponse
{
    /// <summary>响应内容</summary>
    public required string Content { get; init; }

    /// <summary>输入 Token 数</summary>
    public int PromptTokens { get; init; }

    /// <summary>输出 Token 数</summary>
    public int CompletionTokens { get; init; }

    /// <summary>总 Token 数</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>结束原因 (stop, length, content_filter, tool_calls 等)</summary>
    public string? FinishReason { get; init; }

    /// <summary>LLM 返回的工具调用列表（仅当 FinishReason 为 tool_calls 时）</summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>是否包含工具调用</summary>
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}
