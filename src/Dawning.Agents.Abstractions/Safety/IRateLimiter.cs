using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// Rate limiter interface.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to acquire a permit.
    /// </summary>
    /// <param name="key">Rate limit key (e.g., user ID, session ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether execution is allowed.</returns>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Attempts to acquire a permit using a specified policy.
    /// </summary>
    /// <param name="key">Rate limit key (e.g., user ID, session ID).</param>
    /// <param name="policyName">Policy name (null uses the default configuration).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether execution is allowed.</returns>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        string? policyName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the current status.
    /// </summary>
    /// <param name="key">Rate limit key.</param>
    /// <returns>Current rate limit status.</returns>
    RateLimitStatus GetStatus(string key);

    /// <summary>
    /// Gets the current status using a specified policy's limits.
    /// </summary>
    /// <param name="key">Rate limit key.</param>
    /// <param name="policyName">Policy name (null uses the default configuration).</param>
    /// <returns>Current rate limit status.</returns>
    RateLimitStatus GetStatus(string key, string? policyName);

    /// <summary>
    /// Resets the counter for the specified key.
    /// </summary>
    /// <param name="key">Rate limit key.</param>
    void Reset(string key);
}

/// <summary>
/// Token usage rate limiter interface.
/// </summary>
public interface ITokenRateLimiter
{
    /// <summary>
    /// Checks whether the specified number of tokens can be used.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <param name="tokenCount">Token count.</param>
    /// <returns>Rate limit result.</returns>
    TokenRateLimitResult TryUseTokens(string sessionId, int tokenCount);

    /// <summary>
    /// Checks whether the session still has a token budget (without consuming tokens).
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Whether there is remaining budget.</returns>
    bool HasBudget(string sessionId);

    /// <summary>
    /// Gets the number of tokens used by the session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Number of tokens used.</returns>
    int GetUsedTokens(string sessionId);

    /// <summary>
    /// Resets the token counter for the session.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    void ResetSession(string sessionId);
}

/// <summary>
/// Rate limit denial reason.
/// </summary>
public enum RateLimitDenyReason
{
    /// <summary>
    /// Not denied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sliding window rate limit exceeded.
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// Bucket capacity reached (new key denied).
    /// </summary>
    BucketCapReached,

    /// <summary>
    /// Single request token limit exceeded.
    /// </summary>
    TokenPerRequestExceeded,

    /// <summary>
    /// Session total token limit exceeded.
    /// </summary>
    TokenPerSessionExceeded,

    /// <summary>
    /// Token bucket capacity reached.
    /// </summary>
    TokenBucketCapReached,
}

/// <summary>
/// Rate limit result.
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Remaining request count.
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// Reset time.
    /// </summary>
    public DateTimeOffset ResetTime { get; init; }

    /// <summary>
    /// Time to wait before retrying (if rate limited).
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// Denial reason.
    /// </summary>
    public RateLimitDenyReason DenyReason { get; init; }

    /// <summary>
    /// Creates an allow result.
    /// </summary>
    public static RateLimitResult Allow(int remaining, DateTimeOffset resetTime) =>
        new()
        {
            IsAllowed = true,
            RemainingRequests = remaining,
            ResetTime = resetTime,
        };

    /// <summary>
    /// Creates a denial result.
    /// </summary>
    public static RateLimitResult Deny(
        TimeSpan retryAfter,
        DateTimeOffset resetTime,
        RateLimitDenyReason reason = RateLimitDenyReason.RateLimitExceeded
    ) =>
        new()
        {
            IsAllowed = false,
            RemainingRequests = 0,
            ResetTime = resetTime,
            RetryAfter = retryAfter,
            DenyReason = reason,
        };
}

/// <summary>
/// Token rate limit result.
/// </summary>
public record TokenRateLimitResult
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Denial reason.
    /// </summary>
    public RateLimitDenyReason DenyReason { get; init; }

    /// <summary>
    /// Creates an allow result.
    /// </summary>
    public static TokenRateLimitResult Allow() => new() { IsAllowed = true };

    /// <summary>
    /// Creates a denial result.
    /// </summary>
    public static TokenRateLimitResult Deny(RateLimitDenyReason reason) =>
        new() { IsAllowed = false, DenyReason = reason };
}

/// <summary>
/// Rate limit status.
/// </summary>
public record RateLimitStatus
{
    /// <summary>
    /// Number of requests in the current window.
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// Maximum number of requests.
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Window reset time.
    /// </summary>
    public DateTimeOffset ResetTime { get; init; }

    /// <summary>
    /// Whether the rate limit is exceeded.
    /// </summary>
    public bool IsLimited => CurrentCount >= MaxRequests;
}

/// <summary>
/// Rate limit configuration.
/// </summary>
public class RateLimitOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum number of requests per time window.
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 60;

    /// <summary>
    /// Time window size.
    /// </summary>
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum tokens per session.
    /// </summary>
    public int MaxTokensPerSession { get; set; } = 100000;

    /// <summary>
    /// Maximum tokens per request.
    /// </summary>
    public int MaxTokensPerRequest { get; set; } = 4000;

    /// <summary>
    /// Enable rate limiting.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enable backpressure mode (wait instead of immediately rejecting when rate limited).
    /// </summary>
    public bool EnableBackpressure { get; set; }

    /// <summary>
    /// Maximum wait time in backpressure mode.
    /// </summary>
    public TimeSpan BackpressureTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Idle bucket eviction interval.
    /// </summary>
    public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Token bucket idle timeout (buckets not accessed within this duration will be evicted).
    /// </summary>
    public TimeSpan TokenIdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum number of buckets (prevents memory bloat).
    /// </summary>
    public int MaxBuckets { get; set; } = 10_000;

    /// <summary>
    /// Named policies (different keys can bind to different rate limit parameters).
    /// </summary>
    public Dictionary<string, RateLimitPolicy> Policies { get; set; } = [];

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxRequestsPerWindow <= 0)
        {
            throw new InvalidOperationException("MaxRequestsPerWindow must be greater than 0.");
        }

        if (WindowSize <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("WindowSize must be greater than zero.");
        }

        if (MaxTokensPerSession <= 0)
        {
            throw new InvalidOperationException("MaxTokensPerSession must be greater than 0.");
        }

        if (MaxTokensPerRequest <= 0)
        {
            throw new InvalidOperationException("MaxTokensPerRequest must be greater than 0.");
        }

        if (MaxBuckets <= 0)
        {
            throw new InvalidOperationException("MaxBuckets must be greater than 0.");
        }

        if (EvictionInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("EvictionInterval must be greater than zero.");
        }

        if (TokenIdleTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("TokenIdleTimeout must be greater than zero.");
        }

        if (EnableBackpressure && BackpressureTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "BackpressureTimeout must be greater than zero when backpressure is enabled."
            );
        }

        if (MaxTokensPerRequest > MaxTokensPerSession)
        {
            throw new InvalidOperationException(
                "MaxTokensPerRequest must not exceed MaxTokensPerSession."
            );
        }

        foreach (var (name, policy) in Policies)
        {
            policy.Validate(name);
        }
    }
}

/// <summary>
/// Named rate limit policy (overrides default MaxRequestsPerWindow and WindowSize).
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Maximum number of requests per time window.
    /// </summary>
    public int MaxRequestsPerWindow { get; set; }

    /// <summary>
    /// Time window size.
    /// </summary>
    public TimeSpan WindowSize { get; set; }

    /// <summary>
    /// Validates policy parameters.
    /// </summary>
    /// <param name="name">Policy name (used for error messages).</param>
    public void Validate(string name)
    {
        if (MaxRequestsPerWindow <= 0)
        {
            throw new InvalidOperationException(
                $"Policy '{name}': MaxRequestsPerWindow must be greater than 0."
            );
        }

        if (WindowSize <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"Policy '{name}': WindowSize must be greater than zero."
            );
        }
    }
}
