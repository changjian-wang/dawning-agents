namespace DawningAgents.Abstractions.LLM;

/// <summary>
/// 表示对话中的一条消息
/// </summary>
public record ChatMessage(string Role, string Content);

/// <summary>
/// 聊天完成请求的选项
/// </summary>
public record ChatCompletionOptions
{
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 1000;
    public string? SystemPrompt { get; init; }
}

/// <summary>
/// 聊天完成请求的响应
/// </summary>
public record ChatCompletionResponse
{
    public required string Content { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public string? FinishReason { get; init; }
}

/// <summary>
/// LLM 提供者接口（OpenAI、Azure OpenAI、Ollama 等）
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// 提供者名称（如 "OpenAI"、"AzureOpenAI"、"Ollama"）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 发送聊天完成请求
    /// </summary>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式聊天完成响应
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
