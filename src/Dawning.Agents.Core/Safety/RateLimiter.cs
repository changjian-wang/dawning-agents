using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Sliding window rate limiter.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _evictionTimer;
    private readonly Lock _addLock = new();
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

        // Auto-evict idle buckets
        _evictionTimer = _timeProvider.CreateTimer(
            _ =>
            {
                try
                {
                    EvictIdleBuckets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while evicting idle rate limit buckets");
                }
            },
            null,
            _options.EvictionInterval,
            _options.EvictionInterval
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

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
        // Resolve policy: named policy > default configuration
        var maxRequests = _options.MaxRequestsPerWindow;
        var windowSize = _options.WindowSize;

        if (policyName != null && _options.Policies.TryGetValue(policyName, out var policy))
        {
            maxRequests = policy.MaxRequestsPerWindow;
            windowSize = policy.WindowSize;
        }

        var now = _timeProvider.GetUtcNow();

        // Bucket cap protection: double-checked locking to avoid TOCTOU
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            lock (_addLock)
            {
                if (!_buckets.TryGetValue(key, out bucket))
                {
                    if (_buckets.Count >= _options.MaxBuckets)
                    {
                        _logger.LogWarning(
                            "Rate limit bucket cap reached: Count={Count}, MaxBuckets={MaxBuckets}, Key={Key}",
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

                    bucket = new RateLimitBucket(windowSize);
                    _buckets.TryAdd(key, bucket);
                }
            }
        }

        var resetTime = now.Add(windowSize);

        // Atomic operation: cleanup + check + add within the same lock
        var result = bucket.TryAcquire(now, maxRequests, windowSize);

        if (!result.Allowed)
        {
            var retryAfter = result.OldestTimestamp.Add(windowSize) - now;

            _logger.LogWarning(
                "Rate limit triggered: Key={Key}, Count={Count}, MaxRequests={MaxRequests}",
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
            "Rate limit passed: Key={Key}, Remaining={Remaining}/{MaxRequests}",
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

            _logger.LogDebug("Backpressure wait: Key={Key}, WaitTime={WaitTime}", key, waitTime);

            await Task.Delay(waitTime, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        // Last attempt after timeout
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            bucket.CleanupExpired(now, windowSize);

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _buckets.TryRemove(key, out _);
        _logger.LogDebug("Rate limit reset: Key={Key}", key);
    }

    /// <summary>
    /// Evicts idle buckets (buckets with no records within the window are removed).
    /// </summary>
    internal void EvictIdleBuckets()
    {
        var now = _timeProvider.GetUtcNow();

        // Use the maximum window size across default + all named policies.
        // Using only the default window would prematurely evict timestamps
        // for policies with larger windows, effectively bypassing their limits.
        var maxWindowSize = _options.WindowSize;
        foreach (var policy in _options.Policies.Values)
        {
            if (policy.WindowSize > maxWindowSize)
            {
                maxWindowSize = policy.WindowSize;
            }
        }

        var keysToRemove = new List<string>();

        foreach (var kvp in _buckets)
        {
            kvp.Value.CleanupExpired(now, maxWindowSize);
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
            _logger.LogDebug("Evicted {Count} idle rate limit bucket(s)", keysToRemove.Count);
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
    /// Rate limit bucket (sliding window).
    /// </summary>
    private class RateLimitBucket
    {
        private readonly Lock _lock = new();
        private readonly List<DateTimeOffset> _timestamps = [];

        public RateLimitBucket(TimeSpan windowSize)
        {
            // windowSize kept in constructor signature for API compat but not stored;
            // callers pass the window per-call to handle multi-policy keys correctly.
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
        /// Atomic operation: cleanup expired + check limit + add record.
        /// </summary>
        public (bool Allowed, int Count, DateTimeOffset OldestTimestamp) TryAcquire(
            DateTimeOffset now,
            int maxRequests,
            TimeSpan windowSize
        )
        {
            lock (_lock)
            {
                var cutoff = now - windowSize;
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

        public void CleanupExpired(DateTimeOffset now, TimeSpan windowSize)
        {
            var cutoff = now - windowSize;

            lock (_lock)
            {
                _timestamps.RemoveAll(t => t < cutoff);
            }
        }
    }
}

/// <summary>
/// Token usage rate limiter.
/// </summary>
public sealed class TokenRateLimiter : ITokenRateLimiter, IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<TokenRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, TokenUsageBucket> _buckets = new();
    private readonly ITimer _evictionTimer;
    private readonly Lock _addLock = new();
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

        // Auto-evict idle buckets
        _evictionTimer = _timeProvider.CreateTimer(
            _ =>
            {
                try
                {
                    EvictIdleBuckets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while evicting idle token limit buckets");
                }
            },
            null,
            _options.EvictionInterval,
            _options.EvictionInterval
        );
    }

    /// <inheritdoc />
    public TokenRateLimitResult TryUseTokens(string sessionId, int tokenCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenCount);

        if (!_options.Enabled)
        {
            return TokenRateLimitResult.Allow();
        }

        // Check per-request limit
        if (tokenCount > _options.MaxTokensPerRequest)
        {
            _logger.LogWarning(
                "Per-request token limit exceeded: SessionId={SessionId}, Requested={Requested}, Max={Max}",
                sessionId,
                tokenCount,
                _options.MaxTokensPerRequest
            );
            return TokenRateLimitResult.Deny(RateLimitDenyReason.TokenPerRequestExceeded);
        }

        // Bucket cap protection: double-checked locking to avoid TOCTOU
        if (!_buckets.TryGetValue(sessionId, out var bucket))
        {
            lock (_addLock)
            {
                if (!_buckets.TryGetValue(sessionId, out bucket))
                {
                    if (_buckets.Count >= _options.MaxBuckets)
                    {
                        _logger.LogWarning(
                            "Token limit bucket cap reached: Count={Count}, MaxBuckets={MaxBuckets}, SessionId={SessionId}",
                            _buckets.Count,
                            _options.MaxBuckets,
                            sessionId
                        );
                        return TokenRateLimitResult.Deny(RateLimitDenyReason.TokenBucketCapReached);
                    }

                    bucket = new TokenUsageBucket(_timeProvider.GetUtcNow());
                    _buckets.TryAdd(sessionId, bucket);
                }
            }
        }

        // Whether success or failure, mark access time (prevent budget reset when exhausted sessions are evicted due to idle timeout)
        bucket.LastAccessed = _timeProvider.GetUtcNow();

        // Atomic check and add
        if (!bucket.TryAddTokens(tokenCount, _options.MaxTokensPerSession))
        {
            _logger.LogWarning(
                "Session token limit exceeded: SessionId={SessionId}, Current={Current}, Requested={Requested}, Max={Max}",
                sessionId,
                bucket.TotalTokens,
                tokenCount,
                _options.MaxTokensPerSession
            );
            return TokenRateLimitResult.Deny(RateLimitDenyReason.TokenPerSessionExceeded);
        }

        _logger.LogDebug(
            "Token usage: SessionId={SessionId}, Used={Used}, Total={Total}/{Max}",
            sessionId,
            tokenCount,
            bucket.TotalTokens,
            _options.MaxTokensPerSession
        );

        return TokenRateLimitResult.Allow();
    }

    /// <inheritdoc />
    public bool HasBudget(string sessionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_options.Enabled)
        {
            return true;
        }

        if (!_buckets.TryGetValue(sessionId, out var bucket))
        {
            return true; // New session, has budget
        }

        return bucket.TotalTokens < _options.MaxTokensPerSession;
    }

    /// <inheritdoc />
    public int GetUsedTokens(string sessionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return _buckets.TryGetValue(sessionId, out var bucket) ? bucket.TotalTokens : 0;
    }

    /// <summary>
    /// Resets the session.
    /// </summary>
    public void ResetSession(string sessionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        _buckets.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Evicts idle buckets.
    /// </summary>
    internal void EvictIdleBuckets()
    {
        var now = _timeProvider.GetUtcNow();
        var idleThreshold = _options.TokenIdleTimeout;
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
            _logger.LogDebug("Evicted {Count} idle token limit bucket(s)", keysToRemove.Count);
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
