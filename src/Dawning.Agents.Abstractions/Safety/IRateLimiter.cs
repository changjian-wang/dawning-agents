using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// 速率限制器接口
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 尝试获取许可
    /// </summary>
    /// <param name="key">限制键（如用户ID、会话ID）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许执行</returns>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 尝试获取许可（使用指定策略）
    /// </summary>
    /// <param name="key">限制键（如用户ID、会话ID）</param>
    /// <param name="policyName">策略名称（null 则使用默认配置）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否允许执行</returns>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        string? policyName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取当前状态
    /// </summary>
    /// <param name="key">限制键</param>
    /// <returns>当前限制状态</returns>
    RateLimitStatus GetStatus(string key);

    /// <summary>
    /// 获取当前状态（使用指定策略的限额）
    /// </summary>
    /// <param name="key">限制键</param>
    /// <param name="policyName">策略名称（null 则使用默认配置）</param>
    /// <returns>当前限制状态</returns>
    RateLimitStatus GetStatus(string key, string? policyName);

    /// <summary>
    /// 重置指定键的计数器
    /// </summary>
    /// <param name="key">限制键</param>
    void Reset(string key);
}

/// <summary>
/// Token 使用限制器接口
/// </summary>
public interface ITokenRateLimiter
{
    /// <summary>
    /// 检查是否允许使用指定数量的 Token
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="tokenCount">Token 数量</param>
    /// <returns>是否允许</returns>
    bool TryUseTokens(string sessionId, int tokenCount);

    /// <summary>
    /// 获取会话已使用的 Token 数
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <returns>已使用的 Token 数</returns>
    int GetUsedTokens(string sessionId);

    /// <summary>
    /// 重置会话的 Token 计数
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    void ResetSession(string sessionId);
}

/// <summary>
/// 速率限制拒绝原因
/// </summary>
public enum RateLimitDenyReason
{
    /// <summary>
    /// 未被拒绝
    /// </summary>
    None = 0,

    /// <summary>
    /// 滑动窗口速率超限
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// 桶数达到上限（新 key 被拒绝）
    /// </summary>
    BucketCapReached,

    /// <summary>
    /// 单次请求 Token 超限
    /// </summary>
    TokenPerRequestExceeded,

    /// <summary>
    /// 会话 Token 总量超限
    /// </summary>
    TokenPerSessionExceeded,

    /// <summary>
    /// Token 桶数达到上限
    /// </summary>
    TokenBucketCapReached,
}

/// <summary>
/// 速率限制结果
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// 是否允许
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// 剩余请求数
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// 重置时间
    /// </summary>
    public DateTimeOffset ResetTime { get; init; }

    /// <summary>
    /// 需要等待的时间（如果被限制）
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// 拒绝原因
    /// </summary>
    public RateLimitDenyReason DenyReason { get; init; }

    /// <summary>
    /// 创建允许结果
    /// </summary>
    public static RateLimitResult Allow(int remaining, DateTimeOffset resetTime) =>
        new()
        {
            IsAllowed = true,
            RemainingRequests = remaining,
            ResetTime = resetTime,
        };

    /// <summary>
    /// 创建拒绝结果
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
/// 速率限制状态
/// </summary>
public record RateLimitStatus
{
    /// <summary>
    /// 当前窗口内的请求数
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// 最大请求数
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// 窗口重置时间
    /// </summary>
    public DateTimeOffset ResetTime { get; init; }

    /// <summary>
    /// 是否被限制
    /// </summary>
    public bool IsLimited => CurrentCount >= MaxRequests;
}

/// <summary>
/// 速率限制配置
/// </summary>
public class RateLimitOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// 时间窗口内最大请求数
    /// </summary>
    public int MaxRequestsPerWindow { get; set; } = 60;

    /// <summary>
    /// 时间窗口大小
    /// </summary>
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 每个会话的最大 Token 数
    /// </summary>
    public int MaxTokensPerSession { get; set; } = 100000;

    /// <summary>
    /// 每个请求的最大 Token 数
    /// </summary>
    public int MaxTokensPerRequest { get; set; } = 4000;

    /// <summary>
    /// 启用速率限制
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 启用反压模式（被限制时等待而非直接拒绝）
    /// </summary>
    public bool EnableBackpressure { get; set; }

    /// <summary>
    /// 反压模式下的最大等待时间
    /// </summary>
    public TimeSpan BackpressureTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 最大桶数（防止内存膨胀）
    /// </summary>
    public int MaxBuckets { get; set; } = 10_000;

    /// <summary>
    /// 命名策略（不同 key 可绑定不同限流参数）
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

        foreach (var (name, policy) in Policies)
        {
            if (policy.MaxRequestsPerWindow <= 0)
            {
                throw new InvalidOperationException(
                    $"Policy '{name}': MaxRequestsPerWindow must be greater than 0."
                );
            }

            if (policy.WindowSize <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Policy '{name}': WindowSize must be greater than zero."
                );
            }
        }
    }
}

/// <summary>
/// 命名速率限制策略（覆盖默认配置的 MaxRequestsPerWindow 和 WindowSize）
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// 时间窗口内最大请求数
    /// </summary>
    public int MaxRequestsPerWindow { get; set; }

    /// <summary>
    /// 时间窗口大小
    /// </summary>
    public TimeSpan WindowSize { get; set; }
}
