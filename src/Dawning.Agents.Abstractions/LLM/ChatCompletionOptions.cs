namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 聊天完成请求的选项
/// </summary>
public record ChatCompletionOptions
{
    /// <summary>采样温度 (0.0-2.0)，越高越随机</summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>生成的最大 Token 数</summary>
    public int MaxTokens { get; init; } = 1000;

    /// <summary>系统提示词</summary>
    public string? SystemPrompt { get; init; }
}
