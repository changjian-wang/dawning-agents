using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Sliding window memory that retains only the last N messages.
/// </summary>
/// <remarks>
/// <para>Suitable for long conversation scenarios that require controlled memory usage.</para>
/// <para>Automatically discards the oldest messages when the message count exceeds the window size.</para>
/// <para>Uses a <see cref="LinkedList{T}"/> for O(1) head removal performance.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public sealed class WindowMemory : IConversationMemory
{
    private readonly LinkedList<ConversationMessage> _messages = new();
    private readonly ITokenCounter _tokenCounter;
    private readonly int _windowSize;
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public string? SessionId => null;

    /// <summary>
    /// Gets the current message count within the window (maximum is <see cref="WindowSize"/>).
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
    /// Gets the window size.
    /// </summary>
    public int WindowSize => _windowSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowMemory"/> class.
    /// </summary>
    /// <param name="tokenCounter">The token counter.</param>
    /// <param name="windowSize">The window size (maximum number of messages to retain).</param>
    /// <exception cref="ArgumentException">Window size must be a positive number.</exception>
    public WindowMemory(ITokenCounter tokenCounter, int windowSize = 10)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _windowSize =
            windowSize > 0
                ? windowSize
                : throw new ArgumentException(
                    "Window size must be a positive number.",
                    nameof(windowSize)
                );
    }

    /// <summary>
    /// Adds a message to the window. Automatically removes the oldest messages when the window size is exceeded.
    /// </summary>
    public Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }

            _messages.AddLast(message);

            // Trim to window size - O(1) operation
            while (_messages.Count > _windowSize)
            {
                _messages.RemoveFirst();
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a copy of all messages within the window.
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
    /// Gets the message context for LLM calls.
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
                messages = TrimToTokenLimit(maxTokens.Value);
            }

            var result = messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    /// <summary>
    /// Clears all messages within the window.
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
    /// Gets the total token count of all messages within the window.
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_messages.Sum(m => m.TokenCount ?? 0));
        }
    }

    /// <summary>
    /// Trims messages to fit within the token limit, starting from the most recent. Caller must hold _lock.
    /// </summary>
    private IEnumerable<ConversationMessage> TrimToTokenLimit(int maxTokens)
    {
        var result = new LinkedList<ConversationMessage>();
        var tokenCount = 0;

        // Traverse in reverse order starting from the most recent message
        var node = _messages.Last;
        while (node != null)
        {
            var msgTokens = node.Value.TokenCount ?? 0;
            if (tokenCount + msgTokens > maxTokens)
            {
                break;
            }

            result.AddFirst(node.Value);
            tokenCount += msgTokens;
            node = node.Previous;
        }

        return result;
    }
}
