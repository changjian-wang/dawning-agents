using System.Text.Json;
using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Communication;

/// <summary>
/// 基于 Redis Hash + Pub/Sub 的分布式共享状态
/// </summary>
/// <remarks>
/// <para>键值存储使用 Redis Hash，支持原子操作和模式匹配</para>
/// <para>变更通知通过 Redis Pub/Sub 实现跨进程实时推送</para>
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
    /// 初始化 Redis 共享状态
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
            _logger.LogWarning(ex, "反序列化共享状态 {Key} 失败", key);
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

        _logger.LogDebug("设置共享状态 {Key}", key);

        // 通过 Pub/Sub 通知变更
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
            _logger.LogDebug("删除共享状态 {Key}", key);
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

        // 将通配符模式转为简单匹配
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

                // 首次订阅此 key 时创建 Redis 订阅
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
                                _logger.LogError(ex, "共享状态变更处理器执行失败: {Key}", key);
                            }
                        }
                    }
                );
            }

            handlers.Add(handler);
        }

        _logger.LogDebug("订阅共享状态变更: {Key}", key);

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

            _logger.LogDebug("取消订阅共享状态变更: {Key}", key);
        });
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _database.KeyDeleteAsync(_hashKey).ConfigureAwait(false);
        _logger.LogInformation("清除所有共享状态");
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
/// 订阅清理辅助类
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
