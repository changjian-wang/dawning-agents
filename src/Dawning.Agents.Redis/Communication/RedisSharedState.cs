using System.Text.Json;
using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Communication;

/// <summary>
/// A distributed shared state implementation backed by Redis Hash and Pub/Sub.
/// </summary>
/// <remarks>
/// <para>Key-value storage uses Redis Hash, supporting atomic operations and pattern matching.</para>
/// <para>Change notifications use Redis Pub/Sub for cross-process real-time push.</para>
/// </remarks>
public sealed class RedisSharedState : ISharedState, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisSharedState> _logger;
    private readonly string _hashKey;
    private readonly string _channelPrefix;
    private readonly Dictionary<string, List<Action<string, object?>>> _localHandlers = [];
    private readonly object _handlersLock = new();
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisSharedState"/> class.
    /// </summary>
    public RedisSharedState(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options,
        ILogger<RedisSharedState>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger ?? NullLogger<RedisSharedState>.Instance;
        _database = _connection.GetDatabase(_options.DefaultDatabase);
        _subscriber = _connection.GetSubscriber();
        _hashKey = $"{_options.InstanceName}shared_state";
        _channelPrefix = $"{_options.InstanceName}shared_state:change:";
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = await _database.HashGetAsync(_hashKey, key).ConfigureAwait(false);
        if (!value.HasValue)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize shared state for key {Key}", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var json = JsonSerializer.Serialize(value);
        await _database.HashSetAsync(_hashKey, key, json).ConfigureAwait(false);

        _logger.LogDebug("Set shared state {Key}", key);

        // Notify change via Pub/Sub
        await _subscriber
            .PublishAsync(RedisChannel.Literal($"{_channelPrefix}{key}"), json)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var removed = await _database.HashDeleteAsync(_hashKey, key).ConfigureAwait(false);

        if (removed)
        {
            _logger.LogDebug("Deleted shared state {Key}", key);
            await _subscriber
                .PublishAsync(RedisChannel.Literal($"{_channelPrefix}{key}"), "")
                .ConfigureAwait(false);
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _database.HashExistsAsync(_hashKey, key).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetKeysAsync(
        string pattern = "*",
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var allKeys = await _database.HashKeysAsync(_hashKey).ConfigureAwait(false);

        if (pattern == "*")
        {
            return allKeys.Select(k => k.ToString()).ToList();
        }

        // Convert wildcard pattern to simple matching
        var regexPattern =
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern);

        return allKeys.Select(k => k.ToString()).Where(k => regex.IsMatch(k)).ToList();
    }

    /// <inheritdoc />
    public IDisposable OnChange(string key, Action<string, object?> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(handler);

        var channel = RedisChannel.Literal($"{_channelPrefix}{key}");

        lock (_handlersLock)
        {
            if (!_localHandlers.TryGetValue(key, out var handlers))
            {
                handlers = [];
                _localHandlers[key] = handlers;

                // Create Redis subscription on first subscription to this key
                _subscriber.Subscribe(
                    channel,
                    (_, value) =>
                    {
                        List<Action<string, object?>> snapshot;
                        lock (_handlersLock)
                        {
                            if (!_localHandlers.TryGetValue(key, out var h))
                            {
                                return;
                            }

                            snapshot = [.. h];
                        }

                        object? deserialized =
                            value.HasValue && value.ToString().Length > 0
                                ? JsonSerializer.Deserialize<object>(value.ToString())
                                : null;

                        foreach (var h in snapshot)
                        {
                            try
                            {
                                h(key, deserialized);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    ex,
                                    "Shared state change handler failed for key: {Key}",
                                    key
                                );
                            }
                        }
                    }
                );
            }

            handlers.Add(handler);
        }

        _logger.LogDebug("Subscribed to shared state changes: {Key}", key);

        return new Subscription(() =>
        {
            lock (_handlersLock)
            {
                if (_localHandlers.TryGetValue(key, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _localHandlers.Remove(key);
                        _subscriber.Unsubscribe(channel);
                    }
                }
            }

            _logger.LogDebug("Unsubscribed from shared state changes: {Key}", key);
        });
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _database.KeyDeleteAsync(_hashKey).ConfigureAwait(false);
        _logger.LogInformation("Cleared all shared state");
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return (int)_database.HashLength(_hashKey);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_handlersLock)
        {
            foreach (var key in _localHandlers.Keys)
            {
                _subscriber.Unsubscribe(RedisChannel.Literal($"{_channelPrefix}{key}"));
            }

            _localHandlers.Clear();
        }
    }
}

/// <summary>
/// Subscription cleanup helper
/// </summary>
internal sealed class Subscription : IDisposable
{
    private readonly Action _onDispose;
    private int _disposed;

    public Subscription(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _onDispose();
        }
    }
}
