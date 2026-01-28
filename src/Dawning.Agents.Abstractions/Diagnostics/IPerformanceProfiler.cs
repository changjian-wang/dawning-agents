namespace Dawning.Agents.Abstractions.Diagnostics;

/// <summary>
/// 性能分析器接口
/// </summary>
public interface IPerformanceProfiler
{
    /// <summary>
    /// 开始计时一个操作
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="category">操作类别</param>
    /// <returns>计时句柄，Dispose 时结束计时</returns>
    IDisposable StartOperation(string operationName, string? category = null);

    /// <summary>
    /// 记录一个已完成的操作
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <param name="duration">耗时</param>
    /// <param name="category">操作类别</param>
    /// <param name="metadata">附加元数据</param>
    void RecordOperation(
        string operationName,
        TimeSpan duration,
        string? category = null,
        IDictionary<string, object>? metadata = null
    );

    /// <summary>
    /// 获取慢操作列表
    /// </summary>
    /// <param name="threshold">慢操作阈值</param>
    /// <param name="limit">返回数量限制</param>
    IReadOnlyList<OperationTrace> GetSlowOperations(TimeSpan threshold, int limit = 100);

    /// <summary>
    /// 获取操作统计
    /// </summary>
    /// <param name="category">操作类别（可选）</param>
    IReadOnlyDictionary<string, OperationStatistics> GetStatistics(string? category = null);

    /// <summary>
    /// 清除历史记录
    /// </summary>
    void Clear();
}

/// <summary>
/// 操作追踪记录
/// </summary>
public class OperationTrace
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// 操作类别
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 耗时
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public double DurationMs => Duration.TotalMilliseconds;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 操作统计信息
/// </summary>
public class OperationStatistics
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// 总调用次数
    /// </summary>
    public long TotalCount { get; set; }

    /// <summary>
    /// 成功次数
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// 平均耗时
    /// </summary>
    public TimeSpan AverageDuration =>
        TotalCount > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalCount) : TimeSpan.Zero;

    /// <summary>
    /// 最小耗时
    /// </summary>
    public TimeSpan MinDuration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// 最大耗时
    /// </summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 最后一次调用时间
    /// </summary>
    public DateTime LastCallTime { get; set; }
}

/// <summary>
/// 预定义的操作类别
/// </summary>
public static class OperationCategories
{
    /// <summary>
    /// LLM 调用
    /// </summary>
    public const string LLM = "LLM";

    /// <summary>
    /// 工具执行
    /// </summary>
    public const string Tool = "Tool";

    /// <summary>
    /// Agent 执行
    /// </summary>
    public const string Agent = "Agent";

    /// <summary>
    /// RAG 检索
    /// </summary>
    public const string RAG = "RAG";

    /// <summary>
    /// 数据库操作
    /// </summary>
    public const string Database = "Database";

    /// <summary>
    /// HTTP 请求
    /// </summary>
    public const string Http = "Http";

    /// <summary>
    /// 缓存操作
    /// </summary>
    public const string Cache = "Cache";
}
