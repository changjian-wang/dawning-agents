namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// 工作流接口
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// 工作流 ID
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 工作流名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工作流描述
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 所有节点
    /// </summary>
    IReadOnlyList<IWorkflowNode> Nodes { get; }

    /// <summary>
    /// 起始节点 ID
    /// </summary>
    string StartNodeId { get; }

    /// <summary>
    /// 执行工作流
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 工作流节点接口
/// </summary>
public interface IWorkflowNode
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 节点名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 节点类型
    /// </summary>
    WorkflowNodeType Type { get; }

    /// <summary>
    /// 下一个节点 ID（单一后继）
    /// </summary>
    string? NextNodeId { get; }

    /// <summary>
    /// 执行节点
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 工作流节点类型
/// </summary>
public enum WorkflowNodeType
{
    /// <summary>
    /// Agent 节点 - 调用 Agent
    /// </summary>
    Agent,

    /// <summary>
    /// 工具节点 - 直接调用工具
    /// </summary>
    Tool,

    /// <summary>
    /// 条件节点 - 条件分支
    /// </summary>
    Condition,

    /// <summary>
    /// 循环节点 - 循环执行
    /// </summary>
    Loop,

    /// <summary>
    /// 并行节点 - 并行执行多个分支
    /// </summary>
    Parallel,

    /// <summary>
    /// 子工作流节点 - 嵌套工作流
    /// </summary>
    SubWorkflow,

    /// <summary>
    /// 起始节点
    /// </summary>
    Start,

    /// <summary>
    /// 结束节点
    /// </summary>
    End,

    /// <summary>
    /// 人工审批节点
    /// </summary>
    HumanApproval,

    /// <summary>
    /// 延迟节点
    /// </summary>
    Delay,
}

/// <summary>
/// 工作流上下文
/// </summary>
public class WorkflowContext
{
    private readonly Dictionary<string, object?> _state = new();
    private readonly Dictionary<string, NodeExecutionResult> _nodeResults = new();
    private readonly List<WorkflowExecutionStep> _executionHistory = [];
    private readonly Dictionary<string, object?> _metadata = new();

    /// <summary>
    /// 输入数据
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// 共享状态（只读视图）
    /// </summary>
    public IReadOnlyDictionary<string, object?> State => _state;

    /// <summary>
    /// 节点执行结果（只读视图）
    /// </summary>
    public IReadOnlyDictionary<string, NodeExecutionResult> NodeResults => _nodeResults;

    /// <summary>
    /// 执行历史（只读视图）
    /// </summary>
    public IReadOnlyList<WorkflowExecutionStep> ExecutionHistory => _executionHistory;

    /// <summary>
    /// 元数据（只读视图）
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata => _metadata;

    /// <summary>
    /// 获取状态值
    /// </summary>
    public T? GetState<T>(string key)
    {
        if (_state.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// 设置状态值
    /// </summary>
    public void SetState<T>(string key, T value)
    {
        _state[key] = value;
    }

    /// <summary>
    /// 添加节点执行结果
    /// </summary>
    public void AddNodeResult(string nodeId, NodeExecutionResult result)
    {
        _nodeResults[nodeId] = result;
    }

    /// <summary>
    /// 添加执行步骤
    /// </summary>
    public void AddExecutionStep(WorkflowExecutionStep step)
    {
        _executionHistory.Add(step);
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(string key, object? value)
    {
        _metadata[key] = value;
    }

    /// <summary>
    /// 获取上一个节点的结果
    /// </summary>
    public NodeExecutionResult? GetLastResult()
    {
        return _executionHistory.Count > 0
            ? _nodeResults.GetValueOrDefault(_executionHistory[^1].NodeId)
            : null;
    }
}

/// <summary>
/// 节点执行结果
/// </summary>
public record NodeExecutionResult
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// 输出数据
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 下一个节点 ID（用于条件节点决定分支）
    /// </summary>
    public string? NextNodeId { get; init; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }

    /// <summary>创建成功结果</summary>
    public static NodeExecutionResult Ok(string nodeId, string? output = null) =>
        new()
        {
            NodeId = nodeId,
            Success = true,
            Output = output,
        };

    /// <summary>创建失败结果</summary>
    public static NodeExecutionResult Fail(string nodeId, string error) =>
        new()
        {
            NodeId = nodeId,
            Success = false,
            Error = error,
        };

    /// <summary>创建分支结果</summary>
    public static NodeExecutionResult Branch(string nodeId, string nextNodeId) =>
        new()
        {
            NodeId = nodeId,
            Success = true,
            NextNodeId = nextNodeId,
        };
}

/// <summary>
/// 工作流执行步骤
/// </summary>
public record WorkflowExecutionStep
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// 节点类型
    /// </summary>
    public WorkflowNodeType NodeType { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }
}

/// <summary>
/// 工作流执行结果
/// </summary>
public record WorkflowResult
{
    /// <summary>
    /// 工作流 ID
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 最终输出
    /// </summary>
    public string? FinalOutput { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 执行的节点数
    /// </summary>
    public int NodesExecuted { get; init; }

    /// <summary>
    /// 总执行时间（毫秒）
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// 执行历史
    /// </summary>
    public IReadOnlyList<WorkflowExecutionStep>? ExecutionHistory { get; init; }

    /// <summary>
    /// 最终状态
    /// </summary>
    public IReadOnlyDictionary<string, object?>? FinalState { get; init; }
}
