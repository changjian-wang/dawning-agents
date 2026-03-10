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
    private readonly Timer _evictionTimer;
    private bool _disposed;

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
        _evictionTimer = new Timer(
            _ => EvictIdleBuckets(),
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
        if (!_options.Enabled)
        {
            return Task.FromResult(
                RateLimitResult.Allow(_options.MaxRequestsPerWindow, DateTimeOffset.MaxValue)
            );
        }

        var now = _timeProvider.GetUtcNow();
        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket(_options.WindowSize));

        var resetTime = now.Add(_options.WindowSize);

        // 原子操作：清理 + 检查 + 添加 在同一个锁内完成
        var result = bucket.TryAcquire(now, _options.MaxRequestsPerWindow);

        if (!result.Allowed)
        {
            var retryAfter = result.OldestTimestamp.Add(_options.WindowSize) - now;

            _logger.LogWarning(
                "速率限制触发: Key={Key}, Count={Count}, MaxRequests={MaxRequests}",
                key,
                result.Count,
                _options.MaxRequestsPerWindow
            );

            return Task.FromResult(
                RateLimitResult.Deny(
                    retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero,
                    resetTime
                )
            );
        }

        var remaining = _options.MaxRequestsPerWindow - result.Count;

        _logger.LogDebug(
            "速率限制通过: Key={Key}, Remaining={Remaining}/{MaxRequests}",
            key,
            remaining,
            _options.MaxRequestsPerWindow
        );

        return Task.FromResult(RateLimitResult.Allow(remaining, resetTime));
    }

    /// <inheritdoc />
    public RateLimitStatus GetStatus(string key)
    {
        var now = _timeProvider.GetUtcNow();

        if (_buckets.TryGetValue(key, out var bucket))
        {
            bucket.CleanupExpired(now);

            return new RateLimitStatus
            {
                CurrentCount = bucket.Count,
                MaxRequests = _options.MaxRequestsPerWindow,
                ResetTime = now.Add(_options.WindowSize),
            };
        }

        return new RateLimitStatus
        {
            CurrentCount = 0,
            MaxRequests = _options.MaxRequestsPerWindow,
            ResetTime = now.Add(_options.WindowSize),
        };
    }

    /// <inheritdoc />
    public void Reset(string key)
    {
        _buckets.TryRemove(key, out _);
        _logger.LogDebug("速率限制重置: Key={Key}", key);
    }

    /// <summary>
    /// 清理空闲桶（窗口期内无记录的桶会被移除）
    /// </summary>
    public void EvictIdleBuckets()
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
public class TokenRateLimiter
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<TokenRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, TokenUsageBucket> _buckets = new();

    public TokenRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<TokenRateLimiter>? logger = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<TokenRateLimiter>.Instance;
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

        var bucket = _buckets.GetOrAdd(sessionId, _ => new TokenUsageBucket());

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

    private class TokenUsageBucket
    {
        private int _totalTokens;

        public int TotalTokens => Volatile.Read(ref _totalTokens);

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
