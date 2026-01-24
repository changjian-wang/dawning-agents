namespace Dawning.Agents.Abstractions.Scaling;

/// <summary>
/// 扩展配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Scaling": {
///     "MinInstances": 1,
///     "MaxInstances": 10,
///     "TargetCpuPercent": 70,
///     "TargetMemoryPercent": 80
///   }
/// }
/// </code>
/// </remarks>
public record ScalingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Scaling";

    /// <summary>
    /// 最小实例数
    /// </summary>
    public int MinInstances { get; init; } = 1;

    /// <summary>
    /// 最大实例数
    /// </summary>
    public int MaxInstances { get; init; } = 10;

    /// <summary>
    /// 目标 CPU 使用率百分比
    /// </summary>
    public int TargetCpuPercent { get; init; } = 70;

    /// <summary>
    /// 目标内存使用率百分比
    /// </summary>
    public int TargetMemoryPercent { get; init; } = 80;

    /// <summary>
    /// 扩容冷却时间（秒）
    /// </summary>
    public int ScaleUpCooldownSeconds { get; init; } = 60;

    /// <summary>
    /// 缩容冷却时间（秒）
    /// </summary>
    public int ScaleDownCooldownSeconds { get; init; } = 300;

    /// <summary>
    /// 工作队列容量
    /// </summary>
    public int QueueCapacity { get; init; } = 1000;

    /// <summary>
    /// 工作线程数（0 = 自动检测）
    /// </summary>
    public int WorkerCount { get; init; } = 0;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (MinInstances < 1)
        {
            throw new InvalidOperationException("MinInstances must be at least 1");
        }

        if (MaxInstances < MinInstances)
        {
            throw new InvalidOperationException("MaxInstances must be >= MinInstances");
        }

        if (TargetCpuPercent < 1 || TargetCpuPercent > 100)
        {
            throw new InvalidOperationException("TargetCpuPercent must be between 1 and 100");
        }

        if (TargetMemoryPercent < 1 || TargetMemoryPercent > 100)
        {
            throw new InvalidOperationException("TargetMemoryPercent must be between 1 and 100");
        }

        if (ScaleUpCooldownSeconds < 0)
        {
            throw new InvalidOperationException("ScaleUpCooldownSeconds must be non-negative");
        }

        if (ScaleDownCooldownSeconds < 0)
        {
            throw new InvalidOperationException("ScaleDownCooldownSeconds must be non-negative");
        }

        if (QueueCapacity < 1)
        {
            throw new InvalidOperationException("QueueCapacity must be at least 1");
        }

        if (WorkerCount < 0)
        {
            throw new InvalidOperationException("WorkerCount must be non-negative");
        }
    }

    /// <summary>
    /// 获取实际工作线程数
    /// </summary>
    public int GetActualWorkerCount() =>
        WorkerCount > 0 ? WorkerCount : Environment.ProcessorCount * 2;
}

/// <summary>
/// 扩展指标
/// </summary>
public record ScalingMetrics
{
    /// <summary>
    /// CPU 使用率百分比
    /// </summary>
    public double CpuPercent { get; init; }

    /// <summary>
    /// 内存使用率百分比
    /// </summary>
    public double MemoryPercent { get; init; }

    /// <summary>
    /// 队列长度
    /// </summary>
    public int QueueLength { get; init; }

    /// <summary>
    /// 活跃请求数
    /// </summary>
    public int ActiveRequests { get; init; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>
    /// 采集时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 扩展决策
/// </summary>
public record ScalingDecision
{
    /// <summary>
    /// 扩展动作
    /// </summary>
    public ScalingAction Action { get; init; }

    /// <summary>
    /// 实例变化数量
    /// </summary>
    public int Delta { get; init; }

    /// <summary>
    /// 决策原因
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 决策时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 创建无操作决策
    /// </summary>
    public static ScalingDecision None => new() { Action = ScalingAction.None };

    /// <summary>
    /// 创建扩容决策
    /// </summary>
    public static ScalingDecision ScaleUp(int delta, string reason) =>
        new()
        {
            Action = ScalingAction.ScaleUp,
            Delta = delta,
            Reason = reason,
        };

    /// <summary>
    /// 创建缩容决策
    /// </summary>
    public static ScalingDecision ScaleDown(int delta, string reason) =>
        new()
        {
            Action = ScalingAction.ScaleDown,
            Delta = delta,
            Reason = reason,
        };
}

/// <summary>
/// 扩展动作
/// </summary>
public enum ScalingAction
{
    /// <summary>
    /// 无操作
    /// </summary>
    None,

    /// <summary>
    /// 扩容
    /// </summary>
    ScaleUp,

    /// <summary>
    /// 缩容
    /// </summary>
    ScaleDown,
}

/// <summary>
/// 熔断器状态
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// 关闭状态（正常运行）
    /// </summary>
    Closed,

    /// <summary>
    /// 打开状态（阻止请求）
    /// </summary>
    Open,

    /// <summary>
    /// 半开状态（测试恢复）
    /// </summary>
    HalfOpen,
}
