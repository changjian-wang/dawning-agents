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
    /// <summary>
    /// 输入数据
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// 共享状态
    /// </summary>
    public Dictionary<string, object?> State { get; } = new();

    /// <summary>
    /// 节点执行结果
    /// </summary>
    public Dictionary<string, NodeExecutionResult> NodeResults { get; } = new();

    /// <summary>
    /// 执行历史
    /// </summary>
    public List<WorkflowExecutionStep> ExecutionHistory { get; } = [];

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object?> Metadata { get; } = new();

    /// <summary>
    /// 获取状态值
    /// </summary>
    public T? GetState<T>(string key)
    {
        if (State.TryGetValue(key, out var value) && value is T typedValue)
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
        State[key] = value;
    }

    /// <summary>
    /// 获取上一个节点的结果
    /// </summary>
    public NodeExecutionResult? GetLastResult()
    {
        return ExecutionHistory.Count > 0
            ? NodeResults.GetValueOrDefault(ExecutionHistory[^1].NodeId)
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
        new() { NodeId = nodeId, Success = true, Output = output };

    /// <summary>创建失败结果</summary>
    public static NodeExecutionResult Fail(string nodeId, string error) =>
        new() { NodeId = nodeId, Success = false, Error = error };

    /// <summary>创建分支结果</summary>
    public static NodeExecutionResult Branch(string nodeId, string nextNodeId) =>
        new() { NodeId = nodeId, Success = true, NextNodeId = nextNodeId };
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
    public List<WorkflowExecutionStep>? ExecutionHistory { get; init; }

    /// <summary>
    /// 最终状态
    /// </summary>
    public Dictionary<string, object?>? FinalState { get; init; }
}
