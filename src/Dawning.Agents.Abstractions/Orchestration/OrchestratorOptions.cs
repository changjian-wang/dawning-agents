namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// 编排器配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Orchestration": {
///     "MaxConcurrency": 5,
///     "TimeoutSeconds": 300,
///     "ContinueOnError": false,
///     "ResultAggregationStrategy": "LastResult"
///   }
/// }
/// </code>
/// </remarks>
public class OrchestratorOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Orchestration";

    /// <summary>
    /// 最大并发数（用于并行编排）
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// 总超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 单个 Agent 超时时间（秒）
    /// </summary>
    public int AgentTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 某个 Agent 失败后是否继续执行
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// 结果聚合策略（用于并行编排）
    /// </summary>
    public ResultAggregationStrategy AggregationStrategy { get; set; } =
        ResultAggregationStrategy.LastResult;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (MaxConcurrency < 1)
        {
            throw new InvalidOperationException("MaxConcurrency must be at least 1");
        }

        if (TimeoutSeconds < 1)
        {
            throw new InvalidOperationException("TimeoutSeconds must be at least 1");
        }

        if (AgentTimeoutSeconds < 1)
        {
            throw new InvalidOperationException("AgentTimeoutSeconds must be at least 1");
        }
    }
}

/// <summary>
/// 结果聚合策略
/// </summary>
public enum ResultAggregationStrategy
{
    /// <summary>
    /// 使用最后一个 Agent 的结果
    /// </summary>
    LastResult,

    /// <summary>
    /// 使用第一个成功的结果
    /// </summary>
    FirstSuccess,

    /// <summary>
    /// 合并所有结果
    /// </summary>
    Merge,

    /// <summary>
    /// 投票选择最优结果
    /// </summary>
    Vote,

    /// <summary>
    /// 自定义聚合
    /// </summary>
    Custom,
}
