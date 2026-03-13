using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 滑动窗口速率限制器
/// </summary>
public class SlidingWindowRateLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _evictionTimer;
    private volatile bool _disposed;

    public SlidingWindowRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<SlidingWindowRateLimiter>? logger = null,
        TimeProvider? timeProvider = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<SlidingWindowRateLimiter>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Auto-evict idle buckets every 60 seconds
        _evictionTimer = _timeProvider.CreateTimer(
            _ =>
            {
                try
                {
                    EvictIdleBuckets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理空闲速率限制桶时发生错误");
                }
            },
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60)
        );
    }

    /// <inheritdoc />
    public Task<RateLimitResult> TryAcquireAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return TryAcquireAsync(key, policyName: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RateLimitResult> TryAcquireAsync(
        string key,
        string? policyName,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(
                RateLimitResult.Allow(_options.MaxRequestsPerWindow, DateTimeOffset.MaxValue)
            );
        }

        var acquireResult = TryAcquireCore(key, policyName);

        if (!acquireResult.IsAllowed && _options.EnableBackpressure)
        {
            return WaitAndRetryAsync(key, policyName, cancellationToken);
        }

        return Task.FromResult(acquireResult);
    }

    private RateLimitResult TryAcquireCore(string key, string? policyName)
    {
        // 解析策略：命名策略 > 默认配置
        var maxRequests = _options.MaxRequestsPerWindow;
        var windowSize = _options.WindowSize;

        if (policyName != null && _options.Policies.TryGetValue(policyName, out var policy))
        {
            maxRequests = policy.MaxRequestsPerWindow;
            windowSize = policy.WindowSize;
        }

        var now = _timeProvider.GetUtcNow();

        // 桶数上限保护：超过限制时拒绝新 key
        if (!_buckets.ContainsKey(key) && _buckets.Count >= _options.MaxBuckets)
        {
            _logger.LogWarning(
                "速率限制桶数已达上限: Count={Count}, MaxBuckets={MaxBuckets}, Key={Key}",
                _buckets.Count,
                _options.MaxBuckets,
                key
            );

            return RateLimitResult.Deny(
                TimeSpan.FromSeconds(60),
                now.Add(windowSize),
                RateLimitDenyReason.BucketCapReached
            );
        }

        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket(windowSize));

        var resetTime = now.Add(windowSize);

        // 原子操作：清理 + 检查 + 添加 在同一个锁内完成
        var result = bucket.TryAcquire(now, maxRequests);

        if (!result.Allowed)
        {
            var retryAfter = result.OldestTimestamp.Add(windowSize) - now;

            _logger.LogWarning(
                "速率限制触发: Key={Key}, Count={Count}, MaxRequests={MaxRequests}",
                key,
                result.Count,
                maxRequests
            );

            return RateLimitResult.Deny(
                retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero,
                resetTime,
                RateLimitDenyReason.RateLimitExceeded
            );
        }

        var remaining = maxRequests - result.Count;

        _logger.LogDebug(
            "速率限制通过: Key={Key}, Remaining={Remaining}/{MaxRequests}",
            key,
            remaining,
            maxRequests
        );

        return RateLimitResult.Allow(remaining, resetTime);
    }

    private async Task<RateLimitResult> WaitAndRetryAsync(
        string key,
        string? policyName,
        CancellationToken cancellationToken
    )
    {
        var deadline = _timeProvider.GetUtcNow().Add(_options.BackpressureTimeout);

        while (_timeProvider.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = TryAcquireCore(key, policyName);
            if (result.IsAllowed)
            {
                return result;
            }

            var waitTime = result.RetryAfter ?? TimeSpan.FromMilliseconds(500);
            if (waitTime > _options.BackpressureTimeout)
            {
                waitTime = _options.BackpressureTimeout;
            }

            _logger.LogDebug("反压等待: Key={Key}, WaitTime={WaitTime}", key, waitTime);

            await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        // 超时后最后一次尝试
        return TryAcquireCore(key, policyName);
    }

    /// <inheritdoc />
    public RateLimitStatus GetStatus(string key)
    {
        return GetStatus(key, policyName: null);
    }

    /// <inheritdoc />
    public RateLimitStatus GetStatus(string key, string? policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var maxRequests = _options.MaxRequestsPerWindow;
        var windowSize = _options.WindowSize;

        if (policyName != null && _options.Policies.TryGetValue(policyName, out var policy))
        {
            maxRequests = policy.MaxRequestsPerWindow;
            windowSize = policy.WindowSize;
        }

        var now = _timeProvider.GetUtcNow();

        if (_buckets.TryGetValue(key, out var bucket))
        {
            bucket.CleanupExpired(now);

            return new RateLimitStatus
            {
                CurrentCount = bucket.Count,
                MaxRequests = maxRequests,
                ResetTime = now.Add(windowSize),
            };
        }

        return new RateLimitStatus
        {
            CurrentCount = 0,
            MaxRequests = maxRequests,
            ResetTime = now.Add(windowSize),
        };
    }

    /// <inheritdoc />
    public void Reset(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _buckets.TryRemove(key, out _);
        _logger.LogDebug("速率限制重置: Key={Key}", key);
    }

    /// <summary>
    /// 清理空闲桶（窗口期内无记录的桶会被移除）
    /// </summary>
    internal void EvictIdleBuckets()
    {
        var now = _timeProvider.GetUtcNow();
        var keysToRemove = new List<string>();

        foreach (var kvp in _buckets)
        {
            kvp.Value.CleanupExpired(now);
            if (kvp.Value.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("清理了 {Count} 个空闲的速率限制桶", keysToRemove.Count);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _evictionTimer.Dispose();
        }
    }

    /// <summary>
    /// 速率限制桶（滑动窗口）
    /// </summary>
    private class RateLimitBucket
    {
        private readonly TimeSpan _windowSize;
        private readonly Lock _lock = new();
        private readonly List<DateTimeOffset> _timestamps = [];

        public RateLimitBucket(TimeSpan windowSize)
        {
            _windowSize = windowSize;
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _timestamps.Count;
                }
            }
        }

        /// <summary>
        /// 原子操作：清理过期 + 检查限额 + 添加记录
        /// </summary>
        public (bool Allowed, int Count, DateTimeOffset OldestTimestamp) TryAcquire(
            DateTimeOffset now,
            int maxRequests
        )
        {
            lock (_lock)
            {
                var cutoff = now - _windowSize;
                _timestamps.RemoveAll(t => t < cutoff);

                if (_timestamps.Count >= maxRequests)
                {
                    var oldest = _timestamps.Count > 0 ? _timestamps[0] : DateTimeOffset.MinValue;
                    return (false, _timestamps.Count, oldest);
                }

                _timestamps.Add(now);
                return (true, _timestamps.Count, DateTimeOffset.MinValue);
            }
        }

        public void CleanupExpired(DateTimeOffset now)
        {
            var cutoff = now - _windowSize;

            lock (_lock)
            {
                _timestamps.RemoveAll(t => t < cutoff);
            }
        }

        public DateTimeOffset GetOldestTimestamp()
        {
            lock (_lock)
            {
                return _timestamps.Count > 0 ? _timestamps[0] : DateTimeOffset.MinValue;
            }
        }
    }
}

/// <summary>
/// Token 使用限制器
/// </summary>
public class TokenRateLimiter : ITokenRateLimiter, IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<TokenRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, TokenUsageBucket> _buckets = new();
    private readonly ITimer _evictionTimer;
    private volatile bool _disposed;

    public TokenRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<TokenRateLimiter>? logger = null,
        TimeProvider? timeProvider = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<TokenRateLimiter>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Auto-evict idle buckets every 60 seconds
        _evictionTimer = _timeProvider.CreateTimer(
            _ =>
            {
                try
                {
                    EvictIdleBuckets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理空闲 Token 限制桶时发生错误");
                }
            },
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60)
        );
    }

    /// <summary>
    /// 检查是否允许使用指定数量的 Token
    /// </summary>
    public bool TryUseTokens(string sessionId, int tokenCount)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        // 检查单次请求限制
        if (tokenCount > _options.MaxTokensPerRequest)
        {
            _logger.LogWarning(
                "单次请求 Token 超限: SessionId={SessionId}, Requested={Requested}, Max={Max}",
                sessionId,
                tokenCount,
                _options.MaxTokensPerRequest
            );
            return false;
        }

        // 桶数上限保护
        if (!_buckets.ContainsKey(sessionId) && _buckets.Count >= _options.MaxBuckets)
        {
            _logger.LogWarning(
                "Token 限制桶数已达上限: Count={Count}, MaxBuckets={MaxBuckets}, SessionId={SessionId}",
                _buckets.Count,
                _options.MaxBuckets,
                sessionId
            );
            return false;
        }

        var bucket = _buckets.GetOrAdd(
            sessionId,
            _ => new TokenUsageBucket(_timeProvider.GetUtcNow())
        );

        // 原子检查并添加
        if (!bucket.TryAddTokens(tokenCount, _options.MaxTokensPerSession))
        {
            _logger.LogWarning(
                "会话 Token 超限: SessionId={SessionId}, Current={Current}, Requested={Requested}, Max={Max}",
                sessionId,
                bucket.TotalTokens,
                tokenCount,
                _options.MaxTokensPerSession
            );
            return false;
        }

        bucket.LastAccessed = _timeProvider.GetUtcNow();

        _logger.LogDebug(
            "Token 使用: SessionId={SessionId}, Used={Used}, Total={Total}/{Max}",
            sessionId,
            tokenCount,
            bucket.TotalTokens,
            _options.MaxTokensPerSession
        );

        return true;
    }

    /// <summary>
    /// 获取会话已使用的 Token 数
    /// </summary>
    public int GetUsedTokens(string sessionId)
    {
        return _buckets.TryGetValue(sessionId, out var bucket) ? bucket.TotalTokens : 0;
    }

    /// <summary>
    /// 重置会话
    /// </summary>
    public void ResetSession(string sessionId)
    {
        _buckets.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// 清理空闲桶（超过 10 分钟未访问的会话桶会被移除）
    /// </summary>
    internal void EvictIdleBuckets()
    {
        var now = _timeProvider.GetUtcNow();
        var idleThreshold = TimeSpan.FromMinutes(10);
        var keysToRemove = new List<string>();

        foreach (var kvp in _buckets)
        {
            if (now - kvp.Value.LastAccessed > idleThreshold)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("清理了 {Count} 个空闲的 Token 限制桶", keysToRemove.Count);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _evictionTimer.Dispose();
        }
    }

    private class TokenUsageBucket
    {
        private int _totalTokens;
        private long _lastAccessedTicks;

        public TokenUsageBucket(DateTimeOffset createdAt)
        {
            _lastAccessedTicks = createdAt.UtcTicks;
        }

        public int TotalTokens => Volatile.Read(ref _totalTokens);

        public DateTimeOffset LastAccessed
        {
            get => new DateTimeOffset(Volatile.Read(ref _lastAccessedTicks), TimeSpan.Zero);
            set => Volatile.Write(ref _lastAccessedTicks, value.UtcTicks);
        }

        public bool TryAddTokens(int count, int maxTokens)
        {
            while (true)
            {
                var current = Volatile.Read(ref _totalTokens);
                if (count < 0 || current > maxTokens - count)
                {
                    return false;
                }

                if (
                    Interlocked.CompareExchange(ref _totalTokens, current + count, current)
                    == current
                )
                {
                    return true;
                }
            }
        }
    }
}
