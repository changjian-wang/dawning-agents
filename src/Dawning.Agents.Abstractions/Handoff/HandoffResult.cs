using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff 结果
/// </summary>
public record HandoffResult
{
    /// <summary>
    /// 是否成功完成 Handoff
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 执行任务的 Agent 名称
    /// </summary>
    public required string ExecutedByAgent { get; init; }

    /// <summary>
    /// Agent 响应
    /// </summary>
    public AgentResponse? Response { get; init; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Handoff 链路记录（从源 Agent 到最终 Agent 的路径）
    /// </summary>
    public IReadOnlyList<HandoffRecord> HandoffChain { get; init; } = [];

    /// <summary>
    /// 总执行时间
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static HandoffResult Successful(
        string executedBy,
        AgentResponse response,
        IReadOnlyList<HandoffRecord> chain,
        TimeSpan duration
    ) =>
        new()
        {
            Success = true,
            ExecutedByAgent = executedBy,
            Response = response,
            HandoffChain = chain,
            TotalDuration = duration,
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static HandoffResult Failed(
        string executedBy,
        string error,
        IReadOnlyList<HandoffRecord> chain,
        TimeSpan duration
    ) =>
        new()
        {
            Success = false,
            ExecutedByAgent = executedBy,
            Error = error,
            HandoffChain = chain,
            TotalDuration = duration,
        };
}

/// <summary>
/// Handoff 记录 - 记录每次转交的详情
/// </summary>
public record HandoffRecord
{
    /// <summary>
    /// 源 Agent 名称（null 表示初始请求）
    /// </summary>
    public string? FromAgent { get; init; }

    /// <summary>
    /// 目标 Agent 名称
    /// </summary>
    public required string ToAgent { get; init; }

    /// <summary>
    /// 转交原因
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 转交时间
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 传递的输入
    /// </summary>
    public required string Input { get; init; }
}
