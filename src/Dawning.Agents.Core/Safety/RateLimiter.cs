using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 滑动窗口速率限制器
/// </summary>
public class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly TimeProvider _timeProvider;

    public SlidingWindowRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<SlidingWindowRateLimiter>? logger = null,
        TimeProvider? timeProvider = null
    )
    {
        _options = options.Value;
        _logger = logger ?? NullLogger<SlidingWindowRateLimiter>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        // 清理过期的请求记录
        bucket.CleanupExpired(now);

        var resetTime = now.Add(_options.WindowSize);

        if (bucket.Count >= _options.MaxRequestsPerWindow)
        {
            var oldestRequest = bucket.GetOldestTimestamp();
            var retryAfter = oldestRequest.Add(_options.WindowSize) - now;

            _logger.LogWarning(
                "速率限制触发: Key={Key}, Count={Count}, MaxRequests={MaxRequests}",
                key,
                bucket.Count,
                _options.MaxRequestsPerWindow
            );

            return Task.FromResult(
                RateLimitResult.Deny(
                    retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero,
                    resetTime
                )
            );
        }

        bucket.Add(now);

        var remaining = _options.MaxRequestsPerWindow - bucket.Count;

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
    /// 速率限制桶（滑动窗口）
    /// </summary>
    private class RateLimitBucket
    {
        private readonly TimeSpan _windowSize;
        private readonly object _lock = new();
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

        public void Add(DateTimeOffset timestamp)
        {
            lock (_lock)
            {
                _timestamps.Add(timestamp);
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

        // 检查会话总量限制
        if (bucket.TotalTokens + tokenCount > _options.MaxTokensPerSession)
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

        bucket.AddTokens(tokenCount);

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

        public int TotalTokens => _totalTokens;

        public void AddTokens(int count)
        {
            Interlocked.Add(ref _totalTokens, count);
        }
    }
}
