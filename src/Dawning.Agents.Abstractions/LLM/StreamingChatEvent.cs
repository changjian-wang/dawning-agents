namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 流式聊天事件（结构化的流式输出）
/// </summary>
/// <remarks>
/// 替代原始 <c>IAsyncEnumerable&lt;string&gt;</c>，提供 content delta、tool call delta、
/// finish reason 和 token usage 等结构化信息。
/// </remarks>
public record StreamingChatEvent
{
    /// <summary>文本增量内容</summary>
    public string? ContentDelta { get; init; }

    /// <summary>工具调用增量（流式累积 tool call 信息）</summary>
    public ToolCallDelta? ToolCallDelta { get; init; }

    /// <summary>结束原因（仅在最后一个事件中出现）</summary>
    public string? FinishReason { get; init; }

    /// <summary>Token 用量（仅在最后一个事件中出现）</summary>
    public StreamingTokenUsage? Usage { get; init; }

    /// <summary>创建内容增量事件</summary>
    public static StreamingChatEvent Content(string text) => new() { ContentDelta = text };

    /// <summary>创建工具调用增量事件</summary>
    public static StreamingChatEvent ToolCall(ToolCallDelta delta) =>
        new() { ToolCallDelta = delta };

    /// <summary>创建完成事件（含 finish reason 和可选 usage）</summary>
    public static StreamingChatEvent Done(string finishReason, StreamingTokenUsage? usage = null) =>
        new() { FinishReason = finishReason, Usage = usage };
}

/// <summary>
/// 流式工具调用增量
/// </summary>
/// <remarks>
/// 流式响应中 tool call 信息可能分多个 chunk 到达，
/// 需要客户端根据 Index 累积组装完整的 ToolCall。
/// </remarks>
public record ToolCallDelta
{
    /// <summary>工具调用在列表中的索引</summary>
    public int Index { get; init; }

    /// <summary>工具调用 ID（通常在第一个 delta 中出现）</summary>
    public string? Id { get; init; }

    /// <summary>函数名（通常在第一个 delta 中出现）</summary>
    public string? FunctionName { get; init; }

    /// <summary>参数增量（JSON 片段，需要累积拼接）</summary>
    public string? ArgumentsDelta { get; init; }
}

/// <summary>
/// 流式响应的 Token 用量统计
/// </summary>
public record StreamingTokenUsage
{
    /// <summary>输入 Token 数</summary>
    public int PromptTokens { get; init; }

    /// <summary>输出 Token 数</summary>
    public int CompletionTokens { get; init; }

    /// <summary>总 Token 数</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
