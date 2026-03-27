using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Simple buffer memory that stores all messages.
/// </summary>
/// <remarks>
/// <para>The simplest memory implementation that stores all messages in a list.</para>
/// <para>Suitable for short conversations or testing scenarios.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public class BufferMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly ITokenCounter _tokenCounter;
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public string? SessionId => null;

    /// <summary>
    /// Gets the current stored message count.
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferMemory"/> class.
    /// </summary>
    /// <param name="tokenCounter">The token counter.</param>
    public BufferMemory(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
    }

    /// <summary>
    /// Adds a message to the buffer. Automatically calculates the token count if not provided.
    /// </summary>
    public Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            // Calculate token count if not provided
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }

            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a copy of all messages in the buffer.
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_messages.ToList());
        }
    }

    /// <summary>
    /// Gets the message context for LLM calls, optionally trimmed by token limit.
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            IEnumerable<ConversationMessage> messages = _messages;

            if (maxTokens.HasValue)
            {
                // Take the most recent messages that fit within the token limit
                messages = TrimToTokenLimit(_messages, maxTokens.Value);
            }

            var result = messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    /// <summary>
    /// Clears all messages in the buffer.
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _messages.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the total token count of all messages in the buffer.
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var total = _messages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(total);
        }
    }

    /// <summary>
    /// Trims messages to fit within the token limit.
    /// </summary>
    private static IEnumerable<ConversationMessage> TrimToTokenLimit(
        List<ConversationMessage> messages,
        int maxTokens
    )
    {
        var result = new List<ConversationMessage>();
        var tokenCount = 0;

        // Start from the most recent messages
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msgTokens = messages[i].TokenCount ?? 0;
            if (tokenCount + msgTokens > maxTokens)
            {
                break;
            }

            result.Insert(0, messages[i]);
            tokenCount += msgTokens;
        }

        return result;
    }
}
