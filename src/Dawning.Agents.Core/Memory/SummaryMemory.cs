using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Memory that saves tokens by summarizing older messages.
/// </summary>
/// <remarks>
/// <para>Automatically summarizes older messages into a brief summary when the message count exceeds the threshold.</para>
/// <para>Suitable for scenarios that require maintaining long-term context while controlling token consumption.</para>
/// <para>Requires an LLM provider to generate summaries.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public sealed class SummaryMemory : IConversationMemory, IDisposable
{
    private readonly List<ConversationMessage> _recentMessages = [];
    private string _summary = string.Empty;
    private readonly ILLMProvider _llm;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _maxRecentMessages;
    private readonly int _summaryThreshold;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _summarySemaphore = new(1, 1);
    private readonly ILogger<SummaryMemory> _logger;
    private volatile bool _disposed;

    /// <summary>
    /// Gets the message count (including the summary system message and recent messages).
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count + (string.IsNullOrEmpty(_summary) ? 0 : 1);
            }
        }
    }

    /// <summary>
    /// Gets the current summary content.
    /// </summary>
    public string Summary
    {
        get
        {
            lock (_lock)
            {
                return _summary;
            }
        }
    }

    /// <summary>
    /// Gets the maximum number of recent messages to retain.
    /// </summary>
    public int MaxRecentMessages => _maxRecentMessages;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryMemory"/> class.
    /// </summary>
    /// <param name="llm">The LLM provider used to generate summaries.</param>
    /// <param name="tokenCounter">The token counter.</param>
    /// <param name="maxRecentMessages">The number of recent messages to retain. Defaults to 6.</param>
    /// <param name="summaryThreshold">The message count threshold that triggers summarization. Defaults to 10.</param>
    /// <param name="logger">An optional logger instance.</param>
    public SummaryMemory(
        ILLMProvider llm,
        ITokenCounter tokenCounter,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        ILogger<SummaryMemory>? logger = null
    )
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _maxRecentMessages =
            maxRecentMessages > 0
                ? maxRecentMessages
                : throw new ArgumentException(
                    "Max recent messages must be a positive number.",
                    nameof(maxRecentMessages)
                );
        _summaryThreshold =
            summaryThreshold > maxRecentMessages
                ? summaryThreshold
                : throw new ArgumentException(
                    "Summary threshold must be greater than max recent messages.",
                    nameof(summaryThreshold)
                );
        _logger = logger ?? NullLogger<SummaryMemory>.Instance;
    }

    /// <summary>
    /// Adds a message to memory and automatically triggers summarization when the threshold is exceeded.
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ConversationMessage messageWithTokens;
        List<ConversationMessage>? messagesToSummarize = null;

        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                messageWithTokens = message with { TokenCount = tokenCount };
            }
            else
            {
                messageWithTokens = message;
            }

            _recentMessages.Add(messageWithTokens);

            // Check if summarization is needed
            if (_recentMessages.Count >= _summaryThreshold)
            {
                // Take the oldest messages for summarization (retain the recent ones)
                var toSummarize = _recentMessages.Count - _maxRecentMessages;
                messagesToSummarize = _recentMessages.Take(toSummarize).ToList();
                _recentMessages.RemoveRange(0, toSummarize);

                _logger.LogDebug(
                    "Summarization triggered, processing {Count} messages, retaining {Remaining} recent messages",
                    toSummarize,
                    _recentMessages.Count
                );
            }
        }

        // Use semaphore to serialize summarization operations and prevent concurrent summary overwrites
        if (messagesToSummarize != null)
        {
            await _summarySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await SummarizeMessagesAsync(messagesToSummarize, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _summarySemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Generates a summary of messages.
    /// </summary>
    private async Task SummarizeMessagesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken
    )
    {
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));

        string currentSummary;
        lock (_lock)
        {
            currentSummary = _summary;
        }

        var prompt = $"""
            Please summarize the following conversation, preserving key information and context.

            {(string.IsNullOrEmpty(currentSummary) ? "" : $"Previous summary:\n{currentSummary}\n")}
            New messages:
            {conversationText}

            Please provide a concise summary that includes:
            1. Main topics discussed
            2. Key decisions or conclusions
            3. Important context for future conversations

            Summary:
            """;

        try
        {
            var response = await _llm.ChatAsync(
                    [new ChatMessage("user", prompt)],
                    new ChatCompletionOptions { Temperature = 0.3f, MaxTokens = 500 },
                    cancellationToken
                )
                .ConfigureAwait(false);

            lock (_lock)
            {
                _summary = response.Content ?? string.Empty;
            }

            _logger.LogDebug(
                "Summary generated successfully, Length: {Length} characters",
                _summary.Length
            );
        }
        catch (OperationCanceledException)
        {
            // On cancellation, return messages and rethrow to respect the cancellation contract
            lock (_lock)
            {
                _recentMessages.InsertRange(0, messages);
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate summary; original messages will be retained"
            );
            // On summary failure, return messages to the list
            lock (_lock)
            {
                _recentMessages.InsertRange(0, messages);
            }
        }
    }

    /// <summary>
    /// Gets all messages (including the summary and recent messages).
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            var result = new List<ConversationMessage>();

            // If a summary exists, add it as a system message
            if (!string.IsNullOrEmpty(_summary))
            {
                result.Add(
                    new ConversationMessage
                    {
                        Role = "system",
                        Content = $"Summary of previous conversation: {_summary}",
                        TokenCount = _tokenCounter.CountTokens(_summary),
                    }
                );
            }

            result.AddRange(_recentMessages);
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(result);
        }
    }

    /// <summary>
    /// Gets the message context for LLM calls (including summary and recent messages).
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var messages = await GetMessagesAsync(cancellationToken).ConfigureAwait(false);
        var chatMessages = messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

        if (maxTokens.HasValue)
        {
            var budget = maxTokens.Value;
            var result = new List<ChatMessage>();

            // Start from the most recent messages, prioritizing recent context
            for (var i = chatMessages.Count - 1; i >= 0; i--)
            {
                var tokens = _tokenCounter.CountTokens(chatMessages[i].Content);
                if (tokens > budget)
                {
                    break;
                }

                budget -= tokens;
                result.Insert(0, chatMessages[i]);
            }

            return result;
        }

        return chatMessages;
    }

    /// <summary>
    /// Clears all messages and the summary.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Wait for any in-progress summarization to complete, preventing restored messages from overwriting the clear result
        await _summarySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_lock)
            {
                _recentMessages.Clear();
                _summary = string.Empty;
            }
        }
        finally
        {
            _summarySemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the total token count (including summary and recent messages).
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            var recentTokens = _recentMessages.Sum(m => m.TokenCount ?? 0);
            var summaryTokens = string.IsNullOrEmpty(_summary)
                ? 0
                : _tokenCounter.CountTokens(_summary);
            return Task.FromResult(recentTokens + summaryTokens);
        }
    }

    /// <summary>
    /// Releases the semaphore resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _summarySemaphore.Dispose();
    }
}
