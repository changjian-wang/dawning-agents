namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// 编排执行上下文
/// </summary>
public class OrchestrationContext
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 原始用户输入
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// 当前输入（可能被前一个 Agent 修改）
    /// </summary>
    public string CurrentInput { get; set; } = string.Empty;

    /// <summary>
    /// 已执行的 Agent 记录
    /// </summary>
    public List<AgentExecutionRecord> ExecutionHistory { get; } = [];

    /// <summary>
    /// 自定义元数据，可在 Agent 之间传递
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// 是否应该停止执行（用于条件路由）
    /// </summary>
    public bool ShouldStop { get; set; }

    /// <summary>
    /// 停止原因
    /// </summary>
    public string? StopReason { get; set; }
}
