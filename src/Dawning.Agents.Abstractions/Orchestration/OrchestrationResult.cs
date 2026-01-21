namespace Dawning.Agents.Abstractions.Orchestration;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// 编排执行结果
/// </summary>
public record OrchestrationResult
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 最终输出
    /// </summary>
    public string? FinalOutput { get; init; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 每个 Agent 的执行结果
    /// </summary>
    public IReadOnlyList<AgentExecutionRecord> AgentResults { get; init; } = [];

    /// <summary>
    /// 总执行时间
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OrchestrationResult Successful(
        string finalOutput,
        IReadOnlyList<AgentExecutionRecord> agentResults,
        TimeSpan duration,
        IReadOnlyDictionary<string, object>? metadata = null
    ) =>
        new()
        {
            Success = true,
            FinalOutput = finalOutput,
            AgentResults = agentResults,
            Duration = duration,
            Metadata = metadata,
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OrchestrationResult Failed(
        string error,
        IReadOnlyList<AgentExecutionRecord> agentResults,
        TimeSpan duration
    ) =>
        new()
        {
            Success = false,
            Error = error,
            AgentResults = agentResults,
            Duration = duration,
        };
}

/// <summary>
/// 单个 Agent 的执行记录
/// </summary>
public record AgentExecutionRecord
{
    /// <summary>
    /// Agent 名称
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// 输入内容
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Agent 响应
    /// </summary>
    public required AgentResponse Response { get; init; }

    /// <summary>
    /// 执行顺序（从 0 开始）
    /// </summary>
    public int ExecutionOrder { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset EndTime { get; init; }
}
