using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Lock;

/// <summary>
/// 基于 Redis 的分布式锁实现
/// </summary>
/// <remarks>
/// <para>使用 Redis SET NX EX 命令实现互斥锁</para>
/// <para>支持自动续期、可重入等特性</para>
/// </remarks>
public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _database;
    private readonly string _resource;
    private readonly TimeSpan _expiry;
    private readonly ILogger<RedisDistributedLock> _logger;
    private readonly string _lockId;
    private readonly string _lockKey;
    private readonly DistributedLockOptions _options;
    private Timer? _renewalTimer;
    private bool _disposed;

    /// <inheritdoc />
    public string Resource => _resource;

    /// <inheritdoc />
    public string LockId => _lockId;

    /// <inheritdoc />
    public bool IsAcquired { get; private set; }

    /// <inheritdoc />
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// 初始化 Redis 分布式锁
    /// </summary>
    public RedisDistributedLock(
        IDatabase database,
        string resource,
        TimeSpan expiry,
        DistributedLockOptions options,
        ILogger<RedisDistributedLock>? logger = null
    )
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        _expiry = expiry;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<RedisDistributedLock>.Instance;
        _lockId = Guid.NewGuid().ToString("N");
        _lockKey = $"{_options.KeyPrefix}{_resource}";
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (IsAcquired)
        {
            return true;
        }

        var startTime = DateTime.UtcNow;
        var retryInterval = TimeSpan.FromMilliseconds(_options.RetryInterval);

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var acquired = await _database
                .StringSetAsync(_lockKey, _lockId, _expiry, When.NotExists)
                .ConfigureAwait(false);

            if (acquired)
            {
                IsAcquired = true;
                ExpiresAt = DateTime.UtcNow.Add(_expiry);

                if (_options.EnableAutoRenewal)
                {
                    StartRenewalTimer();
                }

                _logger.LogDebug(
                    "Acquired lock {Resource} with ID {LockId}, expires at {ExpiresAt}",
                    _resource,
                    _lockId,
                    ExpiresAt
                );

                return true;
            }

            await Task.Delay(retryInterval, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Failed to acquire lock {Resource} within timeout {Timeout}",
            _resource,
            timeout
        );

        return false;
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAcquired)
        {
            return;
        }

        StopRenewalTimer();

        // 使用 Lua 脚本确保只释放自己持有的锁
        const string script =
            @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
        ";

        try
        {
            var result = await _database
                .ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { _lockKey },
                    new RedisValue[] { _lockId }
                )
                .ConfigureAwait(false);

            if ((int)result == 1)
            {
                _logger.LogDebug("Released lock {Resource} with ID {LockId}", _resource, _lockId);
            }
            else
            {
                _logger.LogWarning(
                    "Lock {Resource} was not held by ID {LockId}, may have expired",
                    _resource,
                    _lockId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock {Resource}", _resource);
            throw;
        }
        finally
        {
            IsAcquired = false;
            ExpiresAt = null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExtendAsync(
        TimeSpan extension,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAcquired)
        {
            return false;
        }

        // 使用 Lua 脚本确保只延长自己持有的锁
        const string script =
            @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('pexpire', KEYS[1], ARGV[2])
            else
                return 0
            end
        ";

        try
        {
            var result = await _database
                .ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { _lockKey },
                    new RedisValue[] { _lockId, (long)extension.TotalMilliseconds }
                )
                .ConfigureAwait(false);

            if ((int)result == 1)
            {
                ExpiresAt = DateTime.UtcNow.Add(extension);
                _logger.LogDebug(
                    "Extended lock {Resource} by {Extension}, new expiry: {ExpiresAt}",
                    _resource,
                    extension,
                    ExpiresAt
                );
                return true;
            }

            _logger.LogWarning(
                "Failed to extend lock {Resource}, may have been released",
                _resource
            );
            IsAcquired = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend lock {Resource}", _resource);
            throw;
        }
    }

    /// <summary>
    /// 启动自动续期定时器
    /// </summary>
    private void StartRenewalTimer()
    {
        var renewalInterval = TimeSpan.FromMilliseconds(
            _expiry.TotalMilliseconds * _options.RenewalInterval
        );

        _renewalTimer = new Timer(
            async _ =>
            {
                try
                {
                    if (IsAcquired)
                    {
                        await ExtendAsync(_expiry).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-renewal failed for lock {Resource}", _resource);
                }
            },
            null,
            renewalInterval,
            renewalInterval
        );
    }

    /// <summary>
    /// 停止自动续期定时器
    /// </summary>
    private void StopRenewalTimer()
    {
        _renewalTimer?.Dispose();
        _renewalTimer = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (IsAcquired)
        {
            await ReleaseAsync().ConfigureAwait(false);
        }

        StopRenewalTimer();
    }
}

/// <summary>
/// Redis 分布式锁工厂
/// </summary>
public sealed class RedisDistributedLockFactory : IDistributedLockFactory
{
    private readonly IConnectionMultiplexer _connection;
    private readonly RedisOptions _redisOptions;
    private readonly DistributedLockOptions _lockOptions;
    private readonly ILogger<RedisDistributedLock> _lockLogger;
    private readonly ILogger<RedisDistributedLockFactory> _logger;

    /// <summary>
    /// 初始化 Redis 分布式锁工厂
    /// </summary>
    public RedisDistributedLockFactory(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> redisOptions,
        IOptions<DistributedLockOptions> lockOptions,
        ILogger<RedisDistributedLock>? lockLogger = null,
        ILogger<RedisDistributedLockFactory>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _redisOptions =
            redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _lockOptions = lockOptions?.Value ?? throw new ArgumentNullException(nameof(lockOptions));
        _lockLogger = lockLogger ?? NullLogger<RedisDistributedLock>.Instance;
        _logger = logger ?? NullLogger<RedisDistributedLockFactory>.Instance;
    }

    /// <inheritdoc />
    public IDistributedLock CreateLock(string resource, TimeSpan expiry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        var database = _connection.GetDatabase(_redisOptions.DefaultDatabase);
        return new RedisDistributedLock(database, resource, expiry, _lockOptions, _lockLogger);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithLockAsync<T>(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentNullException.ThrowIfNull(action);

        await using var lockInstance = CreateLock(resource, expiry);

        var timeout = TimeSpan.FromSeconds(_lockOptions.DefaultWaitTimeout);
        if (!await lockInstance.TryAcquireAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException(
                $"Failed to acquire lock on resource '{resource}' within {timeout}"
            );
        }

        return await action(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    )
    {
        await ExecuteWithLockAsync(
                resource,
                expiry,
                async ct =>
                {
                    await action(ct).ConfigureAwait(false);
                    return true;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
