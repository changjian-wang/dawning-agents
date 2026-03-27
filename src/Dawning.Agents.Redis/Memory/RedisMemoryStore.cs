using System.Text.Json;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Memory;

/// <summary>
/// A distributed session memory store backed by Redis.
/// </summary>
/// <remarks>
/// <para>Implements <see cref="IDistributedMemory"/> to provide cross-node session state sharing.</para>
/// <para>Supports session locking, expiration times, token counting, and more.</para>
/// </remarks>
public sealed class RedisMemoryStore : IDistributedMemory, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly RedisOptions _redisOptions;
    private readonly DistributedSessionOptions _sessionOptions;
    private readonly ILogger<RedisMemoryStore> _logger;
    private readonly string _sessionKey;
    private readonly string _lockKey;
    private readonly string _lockId;
    private readonly ITokenCounter? _tokenCounter;
    private volatile bool _hasLock;
    private volatile bool _disposed;

    /// <inheritdoc />
    public string SessionId { get; }

    private int _messageCount;

    /// <inheritdoc />
    public int MessageCount => Volatile.Read(ref _messageCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMemoryStore"/> class.
    /// </summary>
    public RedisMemoryStore(
        IConnectionMultiplexer connection,
        string sessionId,
        IOptions<RedisOptions> redisOptions,
        IOptions<DistributedSessionOptions> sessionOptions,
        ITokenCounter? tokenCounter = null,
        ILogger<RedisMemoryStore>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _redisOptions =
            redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _sessionOptions =
            sessionOptions?.Value ?? throw new ArgumentNullException(nameof(sessionOptions));
        _tokenCounter = tokenCounter;
        _logger = logger ?? NullLogger<RedisMemoryStore>.Instance;

        _database = _connection.GetDatabase(_redisOptions.DefaultDatabase);
        _sessionKey = $"{_redisOptions.InstanceName}{_sessionOptions.KeyPrefix}{sessionId}";
        _lockKey = $"{_sessionKey}:lock";
        _lockId = Guid.NewGuid().ToString("N");
    }

    /// <inheritdoc />
    public async Task<bool> TryLockSessionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var acquired = await _database
            .StringSetAsync(_lockKey, _lockId, timeout, When.NotExists)
            .ConfigureAwait(false);

        if (acquired)
        {
            _hasLock = true;
            _logger.LogDebug("Acquired session lock for {SessionId}", SessionId);
        }

        return acquired;
    }

    /// <inheritdoc />
    public async Task UnlockSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasLock)
        {
            return;
        }

        const string script =
            @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
        ";

        var result = (int)
            await _database
                .ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { _lockKey },
                    new RedisValue[] { _lockId }
                )
                .ConfigureAwait(false);

        if (result == 1)
        {
            _hasLock = false;
            _logger.LogDebug("Released session lock for {SessionId}", SessionId);
        }
        else
        {
            _hasLock = false;
            _logger.LogWarning(
                "Session lock for {SessionId} was not held (expired or stolen)",
                SessionId
            );
        }
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(message);

        var serialized = JsonSerializer.Serialize(message);
        var currentLength = await _database
            .ListRightPushAsync(_sessionKey, serialized)
            .ConfigureAwait(false);

        // If exceeding max messages, remove the oldest ones
        if (currentLength > _sessionOptions.MaxMessages)
        {
            await _database
                .ListTrimAsync(_sessionKey, -_sessionOptions.MaxMessages, -1)
                .ConfigureAwait(false);
            currentLength = _sessionOptions.MaxMessages;
        }

        Volatile.Write(ref _messageCount, (int)currentLength);

        if (_sessionOptions.EnableSlidingExpiry)
        {
            await RefreshExpiryAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Added message to session {SessionId}, total: {Count}",
            SessionId,
            MessageCount
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var values = await _database.ListRangeAsync(_sessionKey).ConfigureAwait(false);
        var messages = new List<ConversationMessage>(values.Length);

        foreach (var value in values)
        {
            if (!value.IsNullOrEmpty)
            {
                var message = JsonSerializer.Deserialize<ConversationMessage>(value.ToString());
                if (message != null)
                {
                    messages.Add(message);
                }
            }
        }

        Volatile.Write(ref _messageCount, messages.Count);
        return messages;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        var messages = await GetMessagesAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ChatMessage>();
        var totalTokens = 0;

        // Start from the most recent message to preserve the latest context
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var msg = messages[i];
            var chatMessage = new ChatMessage(msg.Role, msg.Content);

            if (maxTokens.HasValue && _tokenCounter != null)
            {
                var tokenCount = msg.TokenCount ?? _tokenCounter.CountTokens(msg.Content);
                if (totalTokens + tokenCount > maxTokens.Value)
                {
                    break;
                }

                totalTokens += tokenCount;
            }

            result.Add(chatMessage);
        }

        result.Reverse();
        return result;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(_sessionKey).ConfigureAwait(false);
        Volatile.Write(ref _messageCount, 0);
        _logger.LogDebug("Cleared session {SessionId}", SessionId);
    }

    /// <inheritdoc />
    public async Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        if (_tokenCounter == null)
        {
            return 0;
        }

        var messages = await GetMessagesAsync(cancellationToken).ConfigureAwait(false);
        return messages.Sum(m => m.TokenCount ?? _tokenCounter.CountTokens(m.Content));
    }

    /// <inheritdoc />
    public async Task SetExpiryAsync(TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        await _database.KeyExpireAsync(_sessionKey, expiry).ConfigureAwait(false);
        _logger.LogDebug("Set expiry for session {SessionId}: {Expiry}", SessionId, expiry);
    }

    /// <inheritdoc />
    public async Task RefreshExpiryAsync(CancellationToken cancellationToken = default)
    {
        var expiry = TimeSpan.FromMinutes(_sessionOptions.DefaultExpiry);
        await _database.KeyExpireAsync(_sessionKey, expiry).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(_sessionKey).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hasLock)
        {
            try
            {
                await UnlockSessionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release Redis session lock");
            }
        }
    }
}

/// <summary>
/// A factory for creating <see cref="RedisMemoryStore"/> instances.
/// </summary>
public sealed class RedisMemoryStoreFactory
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IOptions<RedisOptions> _redisOptions;
    private readonly IOptions<DistributedSessionOptions> _sessionOptions;
    private readonly ITokenCounter? _tokenCounter;
    private readonly ILogger<RedisMemoryStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMemoryStoreFactory"/> class.
    /// </summary>
    public RedisMemoryStoreFactory(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> redisOptions,
        IOptions<DistributedSessionOptions> sessionOptions,
        ITokenCounter? tokenCounter = null,
        ILogger<RedisMemoryStore>? logger = null
    )
    {
        _connection = connection;
        _redisOptions = redisOptions;
        _sessionOptions = sessionOptions;
        _tokenCounter = tokenCounter;
        _logger = logger ?? NullLogger<RedisMemoryStore>.Instance;
    }

    /// <summary>
    /// Creates a memory store for the specified session.
    /// </summary>
    public RedisMemoryStore Create(string sessionId)
    {
        return new RedisMemoryStore(
            _connection,
            sessionId,
            _redisOptions,
            _sessionOptions,
            _tokenCounter,
            _logger
        );
    }
}
