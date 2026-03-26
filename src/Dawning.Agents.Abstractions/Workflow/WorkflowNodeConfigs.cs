using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// Agent node configuration
/// </summary>
public record AgentNodeConfig
{
    /// <summary>
    /// Agent name (used for DI resolution)
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Input template (supports variable substitution)
    /// </summary>
    public string? InputTemplate { get; init; }

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// Timeout (milliseconds)
    /// </summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>
/// Tool node configuration
/// </summary>
public record ToolNodeConfig
{
    /// <summary>
    /// Tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Input template (supports variable substitution)
    /// </summary>
    public string? InputTemplate { get; init; }

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int MaxRetries { get; init; } = 0;
}

/// <summary>
/// Condition branch
/// </summary>
public record ConditionBranch
{
    /// <summary>
    /// Condition name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Condition expression (supports simple pattern matching)
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Target node ID.
    /// </summary>
    public required string TargetNodeId { get; init; }
}

/// <summary>
/// Condition node configuration
/// </summary>
public record ConditionNodeConfig
{
    /// <summary>
    /// Input source for condition evaluation
    /// </summary>
    public string? InputSource { get; init; }

    /// <summary>
    /// Branch list
    /// </summary>
    public IReadOnlyList<ConditionBranch> Branches { get; init; } = [];

    /// <summary>
    /// Default branch (when no conditions match)
    /// </summary>
    public string? DefaultBranchNodeId { get; init; }
}

/// <summary>
/// Loop node configuration
/// </summary>
public record LoopNodeConfig
{
    /// <summary>
    /// Loop type
    /// </summary>
    public LoopType LoopType { get; init; } = LoopType.Count;

    /// <summary>
    /// Maximum iteration count
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Loop condition (collection path in ForEach mode, condition expression in While mode)
    /// </summary>
    public string? LoopCondition { get; init; }

    /// <summary>
    /// Start node within the loop body.
    /// </summary>
    public required string BodyStartNodeId { get; init; }

    /// <summary>
    /// Loop variable name
    /// </summary>
    public string LoopVariable { get; init; } = "item";
}

/// <summary>
/// Loop type
/// </summary>
public enum LoopType
{
    /// <summary>
    /// Fixed count loop
    /// </summary>
    Count,

    /// <summary>
    /// Conditional loop
    /// </summary>
    While,

    /// <summary>
    /// Iterate over collection
    /// </summary>
    ForEach,
}

/// <summary>
/// Parallel branch
/// </summary>
public record ParallelBranch
{
    /// <summary>
    /// Branch name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Branch start node ID.
    /// </summary>
    public required string StartNodeId { get; init; }
}

/// <summary>
/// Parallel node configuration
/// </summary>
public record ParallelNodeConfig
{
    /// <summary>
    /// Parallel branch
    /// </summary>
    public IReadOnlyList<ParallelBranch> Branches { get; init; } = [];

    /// <summary>
    /// Wait strategy
    /// </summary>
    public ParallelWaitStrategy WaitStrategy { get; init; } = ParallelWaitStrategy.WaitAll;

    /// <summary>
    /// Result merge strategy
    /// </summary>
    public ParallelMergeStrategy MergeStrategy { get; init; } = ParallelMergeStrategy.Concatenate;

    /// <summary>
    /// Individual branch timeout (milliseconds)
    /// </summary>
    public int? BranchTimeoutMs { get; init; }
}

/// <summary>
/// Parallel wait strategy
/// </summary>
public enum ParallelWaitStrategy
{
    /// <summary>
    /// Wait for all to complete
    /// </summary>
    WaitAll,

    /// <summary>
    /// Complete when any one finishes
    /// </summary>
    WaitAny,

    /// <summary>
    /// Wait for a specified number to complete
    /// </summary>
    WaitN,
}

/// <summary>
/// Parallel result merge strategy
/// </summary>
public enum ParallelMergeStrategy
{
    /// <summary>
    /// Concatenate all results
    /// </summary>
    Concatenate,

    /// <summary>
    /// Take only the first result
    /// </summary>
    First,

    /// <summary>
    /// Take the last result
    /// </summary>
    Last,

    /// <summary>
    /// Custom merge
    /// </summary>
    Custom,
}

/// <summary>
/// Sub-workflow node configuration
/// </summary>
public record SubWorkflowNodeConfig
{
    /// <summary>
    /// Sub-workflow ID
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Input mapping (parent context key -> child context key)
    /// </summary>
    public Dictionary<string, string>? InputMapping { get; init; }

    /// <summary>
    /// Output mapping (child context key -> parent context key)
    /// </summary>
    public Dictionary<string, string>? OutputMapping { get; init; }
}

/// <summary>
/// Human approval node configuration
/// </summary>
public record HumanApprovalNodeConfig
{
    /// <summary>
    /// Approval prompt message
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Timeout (milliseconds)
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Next node after approval
    /// </summary>
    public required string ApprovedNodeId { get; init; }

    /// <summary>
    /// Next node after rejection
    /// </summary>
    public string? RejectedNodeId { get; init; }

    /// <summary>
    /// Next node after timeout
    /// </summary>
    public string? TimeoutNodeId { get; init; }
}

/// <summary>
/// Delay node configuration
/// </summary>
public record DelayNodeConfig
{
    /// <summary>
    /// Delay duration (milliseconds)
    /// </summary>
    public int DelayMs { get; init; } = 1000;
}
