using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Conversation memory management interface.
/// </summary>
/// <remarks>
/// <para>The Memory system manages an Agent's conversation history and context.</para>
/// <para>Implementation types include: BufferMemory, WindowMemory, SummaryMemory.</para>
/// </remarks>
public interface IConversationMemory
{
    /// <summary>
    /// Adds a message to memory.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all messages in memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message list.</returns>
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets messages formatted as LLM context.
    /// </summary>
    /// <param name="maxTokens">Maximum token count limit (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message list ready for direct use in LLM calls.</returns>
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Clears all messages in memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current token count.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total token count of the current memory.</returns>
    Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the message count.
    /// </summary>
    int MessageCount { get; }
}
