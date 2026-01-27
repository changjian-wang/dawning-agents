using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Security;

/// <summary>
/// 速率限制配置
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// 是否启用速率限制
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认每分钟请求数限制
    /// </summary>
    public int DefaultRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// 滑动窗口大小（秒）
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用令牌桶
    /// </summary>
    public bool UseTokenBucket { get; set; } = false;

    /// <summary>
    /// 令牌桶容量
    /// </summary>
    public int TokenBucketCapacity { get; set; } = 100;

    /// <summary>
    /// 令牌填充速率（每秒）
    /// </summary>
    public double TokenRefillRatePerSecond { get; set; } = 10;
}

/// <summary>
/// 速率限制结果
/// </summary>
public sealed record RateLimitResult
{
    public bool IsAllowed { get; init; }
    public int RemainingRequests { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public string? Reason { get; init; }

    public static RateLimitResult Allowed(int remaining) => new()
    {
        IsAllowed = true,
        RemainingRequests = remaining,
    };

    public static RateLimitResult Denied(TimeSpan retryAfter, string reason) => new()
    {
        IsAllowed = false,
        RemainingRequests = 0,
        RetryAfter = retryAfter,
        Reason = reason,
    };
}

/// <summary>
/// 速率限制器接口
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 检查是否允许请求
    /// </summary>
    Task<RateLimitResult> CheckAsync(
        string key,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置限制
    /// </summary>
    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// 滑动窗口速率限制器
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly RateLimitOptions _options;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;

    public SlidingWindowRateLimiter(
        IOptions<RateLimitOptions>? options = null,
        ILogger<SlidingWindowRateLimiter>? logger = null)
    {
        _options = options?.Value ?? new RateLimitOptions();
        _logger = logger ?? NullLogger<SlidingWindowRateLimiter>.Instance;
    }

    public Task<RateLimitResult> CheckAsync(
        string key,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(RateLimitResult.Allowed(int.MaxValue));
        }

        var maxRequests = limit ?? _options.DefaultRequestsPerMinute;
        var windowSize = TimeSpan.FromSeconds(_options.WindowSizeSeconds);
        var now = DateTimeOffset.UtcNow;

        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket(maxRequests, windowSize));

        lock (bucket)
        {
            // 清理过期的请求
            bucket.CleanupExpired(now);

            if (bucket.RequestCount >= maxRequests)
            {
                var oldestRequest = bucket.OldestRequestTime;
                var retryAfter = oldestRequest.Add(windowSize) - now;

                _logger.LogWarning("速率限制触发: Key={Key}, Count={Count}, Limit={Limit}",
                    key, bucket.RequestCount, maxRequests);

                return Task.FromResult(RateLimitResult.Denied(
                    retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1),
                    $"Rate limit exceeded. Max {maxRequests} requests per {_options.WindowSizeSeconds} seconds."));
            }

            bucket.AddRequest(now);
            var remaining = maxRequests - bucket.RequestCount;

            return Task.FromResult(RateLimitResult.Allowed(remaining));
        }
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        _buckets.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private sealed class RateLimitBucket
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _windowSize;
        private readonly ConcurrentQueue<DateTimeOffset> _requests = new();

        public RateLimitBucket(int maxRequests, TimeSpan windowSize)
        {
            _maxRequests = maxRequests;
            _windowSize = windowSize;
        }

        public int RequestCount => _requests.Count;
        public DateTimeOffset OldestRequestTime => _requests.TryPeek(out var time) ? time : DateTimeOffset.UtcNow;

        public void AddRequest(DateTimeOffset time) => _requests.Enqueue(time);

        public void CleanupExpired(DateTimeOffset now)
        {
            var cutoff = now - _windowSize;
            while (_requests.TryPeek(out var time) && time < cutoff)
            {
                _requests.TryDequeue(out _);
            }
        }
    }
}
