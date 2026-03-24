namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 单次工具执行记录
/// </summary>
public record ToolUsageRecord
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// 是否执行成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 任务上下文描述
    /// </summary>
    public string? TaskContext { get; init; }

    /// <summary>
    /// 记录时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 工具效用统计
/// </summary>
public record ToolUsageStats
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// 总调用次数
    /// </summary>
    public int TotalCalls { get; init; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// 成功率 (0-1)
    /// </summary>
    public float SuccessRate => TotalCalls > 0 ? (float)SuccessCount / TotalCalls : 0f;

    /// <summary>
    /// 平均延迟
    /// </summary>
    public TimeSpan AverageLatency { get; init; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTimeOffset LastUsed { get; init; }

    /// <summary>
    /// 最近的错误信息（保留最近 N 条）
    /// </summary>
    public IReadOnlyList<string> RecentErrors { get; init; } = [];
}
