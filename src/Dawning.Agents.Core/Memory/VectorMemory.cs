using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Vector memory that uses vector retrieval to enhance context relevance.
/// </summary>
/// <remarks>
/// <para>Embeds all messages as vectors and stores them in a vector database.</para>
/// <para>Retrieves relevant historical messages based on recent conversation when building context.</para>
/// <para>Implements a retrieval strategy to prevent context loss in long-running tasks.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public class VectorMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _recentMessages = [];
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _recentWindowSize;
    private readonly int _retrieveTopK;
    private readonly float _minRelevanceScore;
    private readonly ILogger<VectorMemory> _logger;
    private readonly Lock _lock = new();
    private readonly string _sessionId;

    /// <summary>
    /// Gets the current message count (recent window + vector store).
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count + _vectorStore.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of recent messages.
    /// </summary>
    public int RecentMessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of messages in the vector store.
    /// </summary>
    public int VectorStoreCount => _vectorStore.Count;

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorMemory"/> class.
    /// </summary>
    /// <param name="vectorStore">The vector store.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="tokenCounter">The token counter.</param>
    /// <param name="recentWindowSize">The number of recent messages to retain. Defaults to 6.</param>
    /// <param name="retrieveTopK">The number of relevant messages to retrieve. Defaults to 5.</param>
    /// <param name="minRelevanceScore">The minimum relevance score threshold. Defaults to 0.5.</param>
    /// <param name="sessionId">An optional session identifier used to isolate different sessions.</param>
    /// <param name="logger">An optional logger instance.</param>
    public VectorMemory(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        ITokenCounter tokenCounter,
        int recentWindowSize = 6,
        int retrieveTopK = 5,
        float minRelevanceScore = 0.5f,
        string? sessionId = null,
        ILogger<VectorMemory>? logger = null
    )
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embeddingProvider =
            embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _recentWindowSize =
            recentWindowSize > 0
                ? recentWindowSize
                : throw new ArgumentException("Window size must be a positive number.", nameof(recentWindowSize));
        _retrieveTopK =
            retrieveTopK > 0
                ? retrieveTopK
                : throw new ArgumentException("Retrieve count must be a positive number.", nameof(retrieveTopK));
        _minRelevanceScore =
            minRelevanceScore >= 0 && minRelevanceScore <= 1
                ? minRelevanceScore
                : throw new ArgumentException(
                    "Relevance score must be between 0 and 1.",
                    nameof(minRelevanceScore)
                );
        _sessionId = sessionId ?? Guid.NewGuid().ToString();
        _logger = logger ?? NullLogger<VectorMemory>.Instance;

        _logger.LogDebug(
            "VectorMemory initialized, Session: {SessionId}, Window: {WindowSize}, TopK: {TopK}",
            _sessionId,
            _recentWindowSize,
            _retrieveTopK
        );
    }

    /// <summary>
    /// Adds a message. Archives older messages to the vector store when the window size is exceeded.
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ConversationMessage messageWithTokens;
        ConversationMessage? messageToArchive = null;

        lock (_lock)
        {
            // Calculate token count
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

            // Check if archiving of older messages is needed
            if (_recentMessages.Count > _recentWindowSize)
            {
                messageToArchive = _recentMessages[0];
                _recentMessages.RemoveAt(0);
            }
        }

        // Perform vector store operations outside the lock
        if (messageToArchive != null)
        {
            await ArchiveMessageAsync(messageToArchive, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Archives a message to the vector store.
    /// </summary>
    private async Task ArchiveMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Generate the embedding vector
            var embedding = await _embeddingProvider
                .EmbedAsync(message.Content, cancellationToken)
                .ConfigureAwait(false);

            // Create the document chunk
            var chunk = new DocumentChunk
            {
                Id = message.Id,
                Content = message.Content,
                Embedding = embedding,
                DocumentId = _sessionId,
                ChunkIndex = 0,
                Metadata = new Dictionary<string, string>
                {
                    ["role"] = message.Role,
                    ["timestamp"] = message.Timestamp.ToString("O"),
                    ["sessionId"] = _sessionId,
                },
            };

            await _vectorStore.AddAsync(chunk, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Message archived to vector store: {MessageId}, Role: {Role}",
                message.Id,
                message.Role
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive message; message will be kept in recent messages: {MessageId}", message.Id);

            // On archive failure, return the message to the recent messages list to prevent data loss
            lock (_lock)
            {
                _recentMessages.Add(message);
            }
        }
    }

    /// <summary>
    /// Gets all recent messages.
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_recentMessages.ToList());
        }
    }

    /// <summary>
    /// Gets the context: relevant history + recent messages.
    /// </summary>
    /// <remarks>
    /// 1. Builds a query from recent messages.
    /// 2. Retrieves relevant history from the vector store.
    /// 3. Merges relevant history (sorted by timestamp) with recent messages.
    /// </remarks>
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyList<ConversationMessage> recentMessages;
        lock (_lock)
        {
            recentMessages = _recentMessages.ToList();
        }

        // If no history exists, return recent messages directly
        if (_vectorStore.Count == 0 || recentMessages.Count == 0)
        {
            return TrimAndConvertMessages(recentMessages, maxTokens);
        }

        // Build query from recent messages
        var query = BuildQueryFromRecentMessages(recentMessages);

        // Retrieve relevant history
        var relevantHistory = await RetrieveRelevantHistoryAsync(query, cancellationToken)
            .ConfigureAwait(false);

        // Merge context
        var context = MergeContext(relevantHistory, recentMessages, maxTokens);

        _logger.LogDebug(
            "Context built: retrieved {RetrievedCount} relevant history items, {RecentCount} recent messages",
            relevantHistory.Count,
            recentMessages.Count
        );

        return context;
    }

    /// <summary>
    /// Builds query text from recent messages.
    /// </summary>
    private static string BuildQueryFromRecentMessages(
        IReadOnlyList<ConversationMessage> recentMessages
    )
    {
        // Use recent user messages as the query
        var userMessages = recentMessages.Where(m => m.Role == "user").TakeLast(3);
        return string.Join(" ", userMessages.Select(m => m.Content));
    }

    /// <summary>
    /// Retrieves relevant history from the vector store.
    /// </summary>
    private async Task<List<ConversationMessage>> RetrieveRelevantHistoryAsync(
        string query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            // Generate query embedding
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // Search for relevant documents
            var results = await _vectorStore
                .SearchAsync(queryEmbedding, _retrieveTopK, _minRelevanceScore, cancellationToken)
                .ConfigureAwait(false);

            // Convert to ConversationMessage and sort by timestamp
            var relevantMessages = results
                .Select(r => new ConversationMessage
                {
                    Id = r.Chunk.Id,
                    Role = r.Chunk.Metadata.GetValueOrDefault("role", "user"),
                    Content = r.Chunk.Content,
                    Timestamp = DateTimeOffset.TryParse(
                        r.Chunk.Metadata.GetValueOrDefault("timestamp", ""),
                        out var ts
                    )
                        ? ts
                        : DateTimeOffset.MinValue,
                    Metadata = new Dictionary<string, object> { ["relevanceScore"] = r.Score },
                })
                .OrderBy(m => m.Timestamp)
                .ToList();

            return relevantMessages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve relevant history");
            return [];
        }
    }

    /// <summary>
    /// Merges relevant history and recent messages.
    /// </summary>
    private IReadOnlyList<ChatMessage> MergeContext(
        List<ConversationMessage> relevantHistory,
        IReadOnlyList<ConversationMessage> recentMessages,
        int? maxTokens
    )
    {
        var allMessages = new List<ConversationMessage>();

        // Add relevant history if available
        if (relevantHistory.Count > 0)
        {
            // Add a system message marking the historical context
            allMessages.Add(
                new ConversationMessage
                {
                    Role = "system",
                    Content = "[The following is historical context relevant to the current conversation]",
                    TokenCount = _tokenCounter.CountTokens("[The following is historical context relevant to the current conversation]"),
                }
            );

            allMessages.AddRange(relevantHistory);

            allMessages.Add(
                new ConversationMessage
                {
                    Role = "system",
                    Content = "[End of historical context; the following is the current conversation]",
                    TokenCount = _tokenCounter.CountTokens("[End of historical context; the following is the current conversation]"),
                }
            );
        }

        // Add recent messages
        allMessages.AddRange(recentMessages);

        return TrimAndConvertMessages(allMessages, maxTokens);
    }

    /// <summary>
    /// Trims and converts messages.
    /// </summary>
    private IReadOnlyList<ChatMessage> TrimAndConvertMessages(
        IReadOnlyList<ConversationMessage> messages,
        int? maxTokens
    )
    {
        if (!maxTokens.HasValue)
        {
            return messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();
        }

        var result = new List<ChatMessage>();
        var tokenCount = 0;

        // Start from the most recent messages and retain as many as possible
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msgTokens =
                messages[i].TokenCount ?? _tokenCounter.CountTokens(messages[i].Content);
            if (tokenCount + msgTokens > maxTokens.Value)
            {
                break;
            }

            result.Insert(0, new ChatMessage(messages[i].Role, messages[i].Content));
            tokenCount += msgTokens;
        }

        return result;
    }

    /// <summary>
    /// Clears all messages and the vector store.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _recentMessages.Clear();
        }

        try
        {
            // Clear only the current session's data
            await _vectorStore
                .DeleteByDocumentIdAsync(_sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear vector store");
        }

        _logger.LogDebug("VectorMemory cleared, Session: {SessionId}", _sessionId);
    }

    /// <summary>
    /// Gets the current token count (recent messages only).
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var total = _recentMessages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(total);
        }
    }
}
