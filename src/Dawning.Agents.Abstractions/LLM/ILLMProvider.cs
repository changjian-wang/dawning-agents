namespace Dawning.Agents.Abstractions.LLM;

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
    /// 流式聊天完成响应（原始文本流）
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 流式聊天完成响应（结构化事件流）
    /// </summary>
    /// <remarks>
    /// <para>返回 <see cref="StreamingChatEvent"/> 序列，包含 content delta、tool call delta、
    /// finish reason 和 token usage 等结构化信息。</para>
    /// <para>默认实现将 <see cref="ChatStreamAsync"/> 的文本流包装为 content delta 事件。
    /// Provider 可覆盖此方法以提供完整的结构化事件。</para>
    /// </remarks>
    IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => ChatStreamAsync(messages, options, cancellationToken)
        .ToStreamingEvents();
}
