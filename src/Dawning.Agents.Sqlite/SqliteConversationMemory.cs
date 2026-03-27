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
    public int MessageCount => GetMessageCountSync();

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

        var tokenCount = message.TokenCount ?? _tokenCounter.CountTokens(message.Content);

        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection
            .ExecuteAsync(
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
                }
            )
            .ConfigureAwait(false);

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
        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await connection
            .QueryAsync<MessageRow>(
                """
                SELECT role AS Role, content AS Content, token_count AS TokenCount, created_at AS CreatedAt
                FROM conversation_messages
                WHERE session_id = @SessionId
                ORDER BY id ASC
                """,
                new { SessionId }
            )
            .ConfigureAwait(false);

        return rows.Select(r => new ConversationMessage
            {
                Role = r.Role,
                Content = r.Content,
                TokenCount = r.TokenCount,
                Timestamp = DateTimeOffset.Parse(r.CreatedAt),
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
                "DELETE FROM conversation_messages WHERE session_id = @SessionId",
                new { SessionId }
            )
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Cleared {Count} messages from session {SessionId}",
            deleted,
            SessionId
        );
    }

    /// <inheritdoc />
    public async Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dbContext
            .CreateConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await connection
            .ExecuteScalarAsync<int>(
                "SELECT COALESCE(SUM(token_count), 0) FROM conversation_messages WHERE session_id = @SessionId",
                new { SessionId }
            )
            .ConfigureAwait(false);
    }

    private int GetMessageCountSync()
    {
        using var connection = _dbContext.CreateConnectionAsync().GetAwaiter().GetResult();

        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM conversation_messages WHERE session_id = @SessionId",
            new { SessionId }
        );
    }

    /// <summary>
    /// Internal row type for Dapper mapping.
    /// </summary>
    private sealed record MessageRow
    {
        public required string Role { get; init; }
        public required string Content { get; init; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style",
            "IDE1006:Naming Styles",
            Justification = "Maps to snake_case column name"
        )]
        public int TokenCount { get; init; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Style",
            "IDE1006:Naming Styles",
            Justification = "Maps to snake_case column name"
        )]
        public required string CreatedAt { get; init; }
    }
}
