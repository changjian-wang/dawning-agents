namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff 请求 - 当 Agent 需要将任务转交给另一个 Agent 时创建
/// </summary>
/// <remarks>
/// Handoff 是多 Agent 协作的核心机制，允许：
/// - Triage Agent 根据用户请求分配给专家 Agent
/// - Agent 在处理过程中发现需要其他专家时主动转交
/// - 链式调用多个 Agent 完成复杂任务
/// </remarks>
public record HandoffRequest
{
    /// <summary>
    /// 目标 Agent 名称
    /// </summary>
    public required string TargetAgentName { get; init; }

    /// <summary>
    /// 转交原因/说明
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 传递给目标 Agent 的输入
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// 附加的上下文数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// 是否保留对话历史
    /// </summary>
    public bool PreserveHistory { get; init; } = true;

    /// <summary>
    /// 创建 Handoff 请求
    /// </summary>
    public static HandoffRequest To(string targetAgent, string input, string? reason = null) =>
        new()
        {
            TargetAgentName = targetAgent,
            Input = input,
            Reason = reason,
        };
}
