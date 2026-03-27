using System.Globalization;
using Dapper;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Sqlite;

/// <summary>
/// SQLite-backed persistent conversation memory.
/// </summary>
/// <remarks>
/// <para>Persists conversation messages to SQLite, scoped by <see cref="SessionId"/>.</para>
/// <para>Suitable for local-first applications that need conversation persistence across restarts.</para>
/// <para>Thread-safe via connection-per-operation pattern.</para>
/// </remarks>
public sealed class SqliteConversationMemory : IConversationMemory
{
    private readonly SqliteDbContext _dbContext;
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<SqliteConversationMemory> _logger;

    /// <inheritdoc />
    public string? SessionId { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the cached count. The count is lazy-initialized from the database
    /// on the first async operation (e.g., <see cref="AddMessageAsync"/>, <see cref="GetMessagesAsync"/>).
    /// </remarks>
    public int MessageCount => Volatile.Read(ref _messageCount);

    private int _messageCount;
    private volatile bool _messageCountInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConversationMemory"/> class.
    /// </summary>
    /// <param name="dbContext">The SQLite database context.</param>
    /// <param name="tokenCounter">The token counter.</param>
    /// <param name="sessionId">The session ID that scopes this memory instance.</param>
    /// <param name="logger">An optional logger instance.</param>
    public SqliteConversationMemory(
        SqliteDbContext dbContext,
        ITokenCounter tokenCounter,
        string sessionId,
        ILogger<SqliteConversationMemory>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(tokenCounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _dbContext = dbContext;
        _tokenCounter = tokenCounter;
        SessionId = sessionId;
        _logger = logger ?? NullLogger<SqliteConversationMemory>.Instance;
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        await EnsureMessageCountInitializedAsync(cancellationToken).ConfigureAwait(false);

        var tokenCount = message.TokenCount ?? _tokenCounter.CountTokens(message.Content);

        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection
            .ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO conversation_messages (session_id, role, content, token_count, created_at)
                    VALUES (@SessionId, @Role, @Content, @TokenCount, @CreatedAt)
                    """,
                    new
                    {
                        SessionId,
                        message.Role,
                        message.Content,
                        TokenCount = tokenCount,
                        CreatedAt = message.Timestamp.UtcDateTime.ToString("o"),
                    },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        Interlocked.Increment(ref _messageCount);

        _logger.LogDebug(
            "Added {Role} message to session {SessionId} ({TokenCount} tokens)",
            message.Role,
            SessionId,
            tokenCount
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        await EnsureMessageCountInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection
            .QueryAsync<MessageRow>(
                new CommandDefinition(
                    """
                    SELECT role AS Role, content AS Content, token_count AS TokenCount, created_at AS CreatedAt
                    FROM conversation_messages
                    WHERE session_id = @SessionId
                    ORDER BY id ASC
                    """,
                    new { SessionId },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        return rows.Select(r => new ConversationMessage
            {
                Role = r.Role,
                Content = r.Content,
                TokenCount = r.TokenCount,
                Timestamp = DateTimeOffset.Parse(r.CreatedAt, CultureInfo.InvariantCulture),
            })
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        var messages = await GetMessagesAsync(cancellationToken).ConfigureAwait(false);

        if (maxTokens is null)
        {
            return messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList().AsReadOnly();
        }

        // Trim from oldest when over token budget
        var result = new List<ChatMessage>();
        var totalTokens = 0;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var tokenCount =
                messages[i].TokenCount ?? _tokenCounter.CountTokens(messages[i].Content);
            if (totalTokens + tokenCount > maxTokens.Value)
            {
                break;
            }

            totalTokens += tokenCount;
            result.Add(new ChatMessage(messages[i].Role, messages[i].Content));
        }

        result.Reverse();
        return result.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var deleted = await connection
            .ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM conversation_messages WHERE session_id = @SessionId",
                    new { SessionId },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        Volatile.Write(ref _messageCount, 0);
        _messageCountInitialized = true;

        _logger.LogInformation(
            "Cleared {Count} messages from session {SessionId}",
            deleted,
            SessionId
        );
    }

    /// <inheritdoc />
    public async Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureMessageCountInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection
            .ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COALESCE(SUM(token_count), 0) FROM conversation_messages WHERE session_id = @SessionId",
                    new { SessionId },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);
    }

    private async Task EnsureMessageCountInitializedAsync(CancellationToken cancellationToken)
    {
        if (_messageCountInitialized)
        {
            return;
        }

        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var count = await connection
            .ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM conversation_messages WHERE session_id = @SessionId",
                    new { SessionId },
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);

        Volatile.Write(ref _messageCount, count);
        _messageCountInitialized = true;
    }

    /// <summary>
    /// Internal row type for Dapper mapping.
    /// </summary>
    private sealed record MessageRow
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
        public int TokenCount { get; init; }
        public required string CreatedAt { get; init; }
    }
}
