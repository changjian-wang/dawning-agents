using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Resilience;

/// <summary>
/// 弹性策略配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Resilience": {
///     "Retry": {
///       "MaxRetryAttempts": 3,
///       "BaseDelayMs": 1000,
///       "UseJitter": true
///     },
///     "CircuitBreaker": {
///       "FailureRatio": 0.5,
///       "SamplingDurationSeconds": 30,
///       "MinimumThroughput": 10,
///       "BreakDurationSeconds": 30
///     },
///     "Timeout": {
///       "TimeoutSeconds": 120
///     }
///   }
/// }
/// </code>
/// </remarks>
public class ResilienceOptions : IValidatableOptions
{
    /// <summary>配置节名称</summary>
    public const string SectionName = "Resilience";

    /// <summary>
    /// 重试策略配置
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// 断路器策略配置
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// 超时策略配置
    /// </summary>
    public TimeoutOptions Timeout { get; set; } = new();

    /// <summary>
    /// 舱壁隔离策略配置
    /// </summary>
    public BulkheadOptions Bulkhead { get; set; } = new();

    /// <inheritdoc />
    public void Validate()
    {
        if (Retry.MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("Retry.MaxRetryAttempts must be >= 0.");
        }

        if (Retry.BaseDelayMs < 0)
        {
            throw new InvalidOperationException("Retry.BaseDelayMs must be >= 0.");
        }

        if (Retry.MaxDelayMs <= 0)
        {
            throw new InvalidOperationException("Retry.MaxDelayMs must be greater than 0.");
        }

        if (Retry.MaxDelayMs < Retry.BaseDelayMs)
        {
            throw new InvalidOperationException(
                "Retry.MaxDelayMs must be greater than or equal to Retry.BaseDelayMs."
            );
        }

        if (CircuitBreaker.FailureRatio <= 0 || CircuitBreaker.FailureRatio > 1)
        {
            throw new InvalidOperationException(
                "CircuitBreaker.FailureRatio must be between 0 (exclusive) and 1 (inclusive)."
            );
        }

        if (CircuitBreaker.BreakDurationSeconds <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreaker.BreakDurationSeconds must be greater than 0."
            );
        }

        if (CircuitBreaker.MinimumThroughput <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreaker.MinimumThroughput must be greater than 0."
            );
        }

        if (CircuitBreaker.SamplingDurationSeconds <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreaker.SamplingDurationSeconds must be greater than 0."
            );
        }

        if (Timeout.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Timeout.TimeoutSeconds must be greater than 0.");
        }

        if (Bulkhead.MaxConcurrency <= 0)
        {
            throw new InvalidOperationException("Bulkhead.MaxConcurrency must be greater than 0.");
        }

        if (Bulkhead.MaxQueuedActions < 0)
        {
            throw new InvalidOperationException("Bulkhead.MaxQueuedActions must be >= 0.");
        }
    }
}

/// <summary>
/// 重试策略配置
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// 基础延迟时间（毫秒）
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否使用抖动（Jitter）避免惊群效应
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// 最大延迟时间（毫秒）
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;
}

/// <summary>
/// 断路器策略配置
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// 是否启用断路器
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 触发断路的失败率（0.0-1.0）
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// 采样时间窗口（秒）
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// 最小请求数（低于此数不触发断路）
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// 断路持续时间（秒）
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;
}

/// <summary>
/// 超时策略配置
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// 是否启用超时
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// 舱壁隔离策略配置
/// </summary>
/// <remarks>
/// 舱壁隔离用于限制并发请求数，防止资源耗尽。
/// </remarks>
public class BulkheadOptions
{
    /// <summary>
    /// 是否启用舱壁隔离
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 最大并发执行数
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// 最大排队等待数
    /// </summary>
    public int MaxQueuedActions { get; set; } = 20;
}
