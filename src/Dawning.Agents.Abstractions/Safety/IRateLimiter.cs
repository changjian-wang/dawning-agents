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
    /// 获取当前状态
    /// </summary>
    /// <param name="key">限制键</param>
    /// <returns>当前限制状态</returns>
    RateLimitStatus GetStatus(string key);

    /// <summary>
    /// 重置指定键的计数器
    /// </summary>
    /// <param name="key">限制键</param>
    void Reset(string key);
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
    public static RateLimitResult Deny(TimeSpan retryAfter, DateTimeOffset resetTime) =>
        new()
        {
            IsAllowed = false,
            RemainingRequests = 0,
            ResetTime = resetTime,
            RetryAfter = retryAfter,
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
public class RateLimitOptions
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
}
