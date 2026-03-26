namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// Workflow interface
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Workflow name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Workflow description
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// All nodes
    /// </summary>
    IReadOnlyList<IWorkflowNode> Nodes { get; }

    /// <summary>
    /// Start node ID
    /// </summary>
    string StartNodeId { get; }

    /// <summary>
    /// Execute the workflow
    /// </summary>
    Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Workflow node interface
/// </summary>
public interface IWorkflowNode
{
    /// <summary>
    /// Node ID
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Node name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Node type
    /// </summary>
    WorkflowNodeType Type { get; }

    /// <summary>
    /// Next node ID (single successor)
    /// </summary>
    string? NextNodeId { get; }

    /// <summary>
    /// Execute the node
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Workflow node type.
/// </summary>
public enum WorkflowNodeType
{
    /// <summary>
    /// Agent node - invokes an Agent
    /// </summary>
    Agent,

    /// <summary>
    /// Tool node - directly invokes a tool
    /// </summary>
    Tool,

    /// <summary>
    /// Condition node - conditional branching
    /// </summary>
    Condition,

    /// <summary>
    /// Loop node - iterative execution
    /// </summary>
    Loop,

    /// <summary>
    /// Parallel node - executes multiple branches concurrently
    /// </summary>
    Parallel,

    /// <summary>
    /// Sub-workflow node - nested workflow
    /// </summary>
    SubWorkflow,

    /// <summary>
    /// Start node
    /// </summary>
    Start,

    /// <summary>
    /// End node
    /// </summary>
    End,

    /// <summary>
    /// Human approval node
    /// </summary>
    HumanApproval,

    /// <summary>
    /// Delay node
    /// </summary>
    Delay,
}

/// <summary>
/// Workflow context (thread-safe)
/// </summary>
public class WorkflowContext
{
    private readonly Dictionary<string, object?> _state = new();
    private readonly Dictionary<string, NodeExecutionResult> _nodeResults = new();
    private readonly List<WorkflowExecutionStep> _executionHistory = [];
    private readonly Dictionary<string, object?> _metadata = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Input data
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Shared state (read-only snapshot)
    /// </summary>
    public IReadOnlyDictionary<string, object?> State
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, object?>(_state);
            }
        }
    }

    /// <summary>
    /// Node execution result（只读快照）
    /// </summary>
    public IReadOnlyDictionary<string, NodeExecutionResult> NodeResults
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, NodeExecutionResult>(_nodeResults);
            }
        }
    }

    /// <summary>
    /// Execution history (read-only snapshot)
    /// </summary>
    public IReadOnlyList<WorkflowExecutionStep> ExecutionHistory
    {
        get
        {
            lock (_lock)
            {
                return _executionHistory.ToList();
            }
        }
    }

    /// <summary>
    /// Metadata（只读快照）
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, object?>(_metadata);
            }
        }
    }

    /// <summary>
    /// Get a state value
    /// </summary>
    public T? GetState<T>(string key)
    {
        lock (_lock)
        {
            if (_state.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }
    }

    /// <summary>
    /// Set a state value
    /// </summary>
    public void SetState<T>(string key, T value)
    {
        lock (_lock)
        {
            _state[key] = value;
        }
    }

    /// <summary>
    /// 添加Node execution result
    /// </summary>
    public void AddNodeResult(string nodeId, NodeExecutionResult result)
    {
        lock (_lock)
        {
            _nodeResults[nodeId] = result;
        }
    }

    /// <summary>
    /// Add an execution step
    /// </summary>
    public void AddExecutionStep(WorkflowExecutionStep step)
    {
        lock (_lock)
        {
            _executionHistory.Add(step);
        }
    }

    /// <summary>
    /// 设置Metadata
    /// </summary>
    public void SetMetadata(string key, object? value)
    {
        lock (_lock)
        {
            _metadata[key] = value;
        }
    }

    /// <summary>
    /// Get the result of the previous node
    /// </summary>
    public NodeExecutionResult? GetLastResult()
    {
        lock (_lock)
        {
            return _executionHistory.Count > 0
                ? _nodeResults.GetValueOrDefault(_executionHistory[^1].NodeId)
                : null;
        }
    }
}

/// <summary>
/// Node execution result
/// </summary>
public record NodeExecutionResult
{
    /// <summary>
    /// Node ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Whether successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Output data
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Next node ID (used by condition nodes to determine branching)
    /// </summary>
    public string? NextNodeId { get; init; }

    /// <summary>
    /// Execution duration (milliseconds)
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Metadata
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; init; }

    /// <summary>Create a success result</summary>
    public static NodeExecutionResult Ok(string nodeId, string? output = null) =>
        new()
        {
            NodeId = nodeId,
            Success = true,
            Output = output,
        };

    /// <summary>Create a failure result</summary>
    public static NodeExecutionResult Fail(string nodeId, string error) =>
        new()
        {
            NodeId = nodeId,
            Success = false,
            Error = error,
        };

    /// <summary>Create a branching result</summary>
    public static NodeExecutionResult Branch(string nodeId, string nextNodeId) =>
        new()
        {
            NodeId = nodeId,
            Success = true,
            NextNodeId = nextNodeId,
        };
}

/// <summary>
/// Workflow execution step
/// </summary>
public record WorkflowExecutionStep
{
    /// <summary>
    /// Node ID
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// Node name
    /// </summary>
    public required string NodeName { get; init; }

    /// <summary>
    /// Node type
    /// </summary>
    public WorkflowNodeType NodeType { get; init; }

    /// <summary>
    /// Start time
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// End time
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Whether successful
    /// </summary>
    public bool Success { get; init; }
}

/// <summary>
/// Workflow execution result
/// </summary>
public record WorkflowResult
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Whether successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Final output
    /// </summary>
    public string? FinalOutput { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Number of nodes executed
    /// </summary>
    public int NodesExecuted { get; init; }

    /// <summary>
    /// Total execution duration (milliseconds)
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// Execution history
    /// </summary>
    public IReadOnlyList<WorkflowExecutionStep>? ExecutionHistory { get; init; }

    /// <summary>
    /// Final state
    /// </summary>
    public IReadOnlyDictionary<string, object?>? FinalState { get; init; }
}
