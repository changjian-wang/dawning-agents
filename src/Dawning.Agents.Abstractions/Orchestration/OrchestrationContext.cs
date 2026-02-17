namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// 编排执行上下文
/// </summary>
public class OrchestrationContext
{
    private readonly List<AgentExecutionRecord> _executionHistory = [];
    private readonly Dictionary<string, object> _metadata = [];

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
    /// 已执行的 Agent 记录（只读视图）
    /// </summary>
    public IReadOnlyList<AgentExecutionRecord> ExecutionHistory => _executionHistory;

    /// <summary>
    /// 自定义元数据，可在 Agent 之间传递（只读视图）
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>
    /// 添加执行记录
    /// </summary>
    public void AddExecutionRecord(AgentExecutionRecord record)
    {
        _executionHistory.Add(record);
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(string key, object value)
    {
        _metadata[key] = value;
    }

    /// <summary>
    /// 是否应该停止执行（用于条件路由）
    /// </summary>
    public bool ShouldStop { get; set; }

    /// <summary>
    /// 停止原因
    /// </summary>
    public string? StopReason { get; set; }
}
