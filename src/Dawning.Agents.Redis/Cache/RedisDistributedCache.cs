using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Cache;

/// <summary>
/// 基于 Redis 的分布式缓存实现
/// </summary>
/// <remarks>
/// <para>提供高性能的分布式缓存能力</para>
/// <para>支持序列化、过期时间、批量操作等特性</para>
/// </remarks>
public sealed class RedisDistributedCache : IDistributedCache, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisDistributedCache> _logger;
    private readonly string _prefix;
    private bool _disposed;

    /// <summary>
    /// 初始化 Redis 分布式缓存
    /// </summary>
    public RedisDistributedCache(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options,
        ILogger<RedisDistributedCache>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<RedisDistributedCache>.Instance;
        _database = _connection.GetDatabase(_options.DefaultDatabase);
        _prefix = _options.InstanceName;
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            var value = _database.StringGet(fullKey);
            return value.HasValue ? (byte[]?)value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            var value = await _database.StringGetAsync(fullKey).ConfigureAwait(false);
            return value.HasValue ? (byte[]?)value : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var fullKey = GetFullKey(key);
        var expiry = GetExpiry(options);

        try
        {
            _database.StringSet(fullKey, value, expiry);
            _logger.LogDebug("Cache set: {Key}, Expiry: {Expiry}", fullKey, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var fullKey = GetFullKey(key);
        var expiry = GetExpiry(options);

        try
        {
            await _database.StringSetAsync(fullKey, value, expiry).ConfigureAwait(false);
            _logger.LogDebug("Cache set: {Key}, Expiry: {Expiry}", fullKey, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public void Refresh(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            // Redis 不支持直接 refresh，需要获取当前 TTL 并重新设置
            var ttl = _database.KeyTimeToLive(fullKey);
            if (ttl.HasValue)
            {
                _database.KeyExpire(fullKey, ttl.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            var ttl = await _database.KeyTimeToLiveAsync(fullKey).ConfigureAwait(false);
            if (ttl.HasValue)
            {
                await _database.KeyExpireAsync(fullKey, ttl.Value).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            _database.KeyDelete(fullKey);
            _logger.LogDebug("Cache removed: {Key}", fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            await _database.KeyDeleteAsync(fullKey).ConfigureAwait(false);
            _logger.LogDebug("Cache removed: {Key}", fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <summary>
    /// 获取完整的缓存 key
    /// </summary>
    private string GetFullKey(string key) => $"{_prefix}cache:{key}";

    /// <summary>
    /// 从配置中获取过期时间
    /// </summary>
    private static TimeSpan? GetExpiry(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return options.AbsoluteExpirationRelativeToNow;
        }

        if (options.AbsoluteExpiration.HasValue)
        {
            return options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
        }

        if (options.SlidingExpiration.HasValue)
        {
            return options.SlidingExpiration;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // IConnectionMultiplexer 是共享的，不在这里释放
    }
}
