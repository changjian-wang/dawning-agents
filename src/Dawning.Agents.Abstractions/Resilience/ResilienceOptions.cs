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
public class ResilienceOptions
{
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
