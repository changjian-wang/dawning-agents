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

    /// <summary>结束原因 (stop, length, content_filter 等)</summary>
    public string? FinishReason { get; init; }
}
