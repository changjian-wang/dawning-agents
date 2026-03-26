namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// LLM provider interface (OpenAI, Azure OpenAI, Ollama, etc.).
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Provider name (e.g., "OpenAI", "AzureOpenAI", "Ollama").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends a chat completion request.
    /// </summary>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Streams a chat completion response (raw text stream).
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Streams a chat completion response (structured event stream).
    /// </summary>
    /// <remarks>
    /// <para>Returns a sequence of <see cref="StreamingChatEvent"/> containing content deltas, tool call deltas,
    /// finish reasons, and token usage as structured information.</para>
    /// <para>The default implementation wraps the text stream from <see cref="ChatStreamAsync"/> as content delta events.
    /// Providers can override this method to provide full structured events.</para>
    /// </remarks>
    IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => ChatStreamAsync(messages, options, cancellationToken).ToStreamingEvents();
}
