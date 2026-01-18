namespace DawningAgents.Abstractions.LLM;

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
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 流式聊天完成响应
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
