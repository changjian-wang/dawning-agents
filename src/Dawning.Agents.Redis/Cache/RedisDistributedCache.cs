using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Cache;

/// <summary>
/// A distributed cache implementation backed by Redis.
/// </summary>
/// <remarks>
/// <para>Provides high-performance distributed caching capabilities.</para>
/// <para>Supports serialization, expiration, batch operations, and more.</para>
/// </remarks>
public sealed class RedisDistributedCache : IDistributedCache, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisDistributedCache> _logger;
    private readonly string _prefix;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisDistributedCache"/> class.
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        var fullKey = GetFullKey(key);
        var expiry = GetExpiry(options);

        try
        {
            _database.StringSet(fullKey, value, expiry);

            if (options.SlidingExpiration.HasValue)
            {
                var slidingMs = (long)options.SlidingExpiration.Value.TotalMilliseconds;
                _database.StringSet(GetSlidingMetadataKey(fullKey), slidingMs, expiry);
            }
            else
            {
                _database.KeyDelete(GetSlidingMetadataKey(fullKey));
            }

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);
        token.ThrowIfCancellationRequested();

        var fullKey = GetFullKey(key);
        var expiry = GetExpiry(options);

        try
        {
            await _database.StringSetAsync(fullKey, value, expiry).ConfigureAwait(false);

            if (options.SlidingExpiration.HasValue)
            {
                var slidingMs = (long)options.SlidingExpiration.Value.TotalMilliseconds;
                await _database
                    .StringSetAsync(GetSlidingMetadataKey(fullKey), slidingMs, expiry)
                    .ConfigureAwait(false);
            }
            else
            {
                await _database
                    .KeyDeleteAsync(GetSlidingMetadataKey(fullKey))
                    .ConfigureAwait(false);
            }

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            var slidingMeta = _database.StringGet(GetSlidingMetadataKey(fullKey));
            if (!slidingMeta.HasValue || !long.TryParse(slidingMeta.ToString(), out var slidingMs))
            {
                return;
            }

            var sliding = TimeSpan.FromMilliseconds(slidingMs);
            _database.KeyExpire(fullKey, sliding);
            _database.KeyExpire(GetSlidingMetadataKey(fullKey), sliding);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();

        var fullKey = GetFullKey(key);
        try
        {
            var slidingMeta = await _database
                .StringGetAsync(GetSlidingMetadataKey(fullKey))
                .ConfigureAwait(false);
            if (!slidingMeta.HasValue || !long.TryParse(slidingMeta.ToString(), out var slidingMs))
            {
                return;
            }

            var sliding = TimeSpan.FromMilliseconds(slidingMs);
            await _database.KeyExpireAsync(fullKey, sliding).ConfigureAwait(false);
            await _database
                .KeyExpireAsync(GetSlidingMetadataKey(fullKey), sliding)
                .ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var fullKey = GetFullKey(key);
        try
        {
            _database.KeyDelete(fullKey);
            _database.KeyDelete(GetSlidingMetadataKey(fullKey));
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        token.ThrowIfCancellationRequested();

        var fullKey = GetFullKey(key);
        try
        {
            await _database.KeyDeleteAsync(fullKey).ConfigureAwait(false);
            await _database.KeyDeleteAsync(GetSlidingMetadataKey(fullKey)).ConfigureAwait(false);
            _logger.LogDebug("Cache removed: {Key}", fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache key: {Key}", fullKey);
            throw;
        }
    }

    /// <summary>
    /// Gets the full cache key.
    /// </summary>
    private string GetFullKey(string key) => $"{_prefix}cache:{key}";

    private static string GetSlidingMetadataKey(string fullKey) => $"{fullKey}:sliding";

    /// <summary>
    /// Gets the expiration from the cache entry options.
    /// </summary>
    private static TimeSpan? GetExpiry(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return options.AbsoluteExpirationRelativeToNow;
        }

        if (options.AbsoluteExpiration.HasValue)
        {
            var expiry = options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
            if (expiry <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "AbsoluteExpiration must be a future time"
                );
            }

            return expiry;
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
        // IConnectionMultiplexer is shared; do not dispose it here
    }
}
