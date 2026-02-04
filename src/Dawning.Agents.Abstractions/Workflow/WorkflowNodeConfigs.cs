using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Abstractions.Workflow;

/// <summary>
/// Agent 节点配置
/// </summary>
public record AgentNodeConfig
{
    /// <summary>
    /// Agent 名称（用于从 DI 解析）
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// 输入模板（支持变量替换）
    /// </summary>
    public string? InputTemplate { get; init; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int? TimeoutMs { get; init; }
}

/// <summary>
/// 工具节点配置
/// </summary>
public record ToolNodeConfig
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// 输入模板（支持变量替换）
    /// </summary>
    public string? InputTemplate { get; init; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 0;
}

/// <summary>
/// 条件分支
/// </summary>
public record ConditionBranch
{
    /// <summary>
    /// 条件名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 条件表达式（支持简单模式匹配）
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public required string TargetNodeId { get; init; }
}

/// <summary>
/// 条件节点配置
/// </summary>
public record ConditionNodeConfig
{
    /// <summary>
    /// 条件判断的输入来源
    /// </summary>
    public string? InputSource { get; init; }

    /// <summary>
    /// 分支列表
    /// </summary>
    public List<ConditionBranch> Branches { get; init; } = [];

    /// <summary>
    /// 默认分支（所有条件都不匹配时）
    /// </summary>
    public string? DefaultBranchNodeId { get; init; }
}

/// <summary>
/// 循环节点配置
/// </summary>
public record LoopNodeConfig
{
    /// <summary>
    /// 循环类型
    /// </summary>
    public LoopType LoopType { get; init; } = LoopType.Count;

    /// <summary>
    /// 最大迭代次数
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// 循环条件（ForEach 模式下是集合路径，While 模式下是条件表达式）
    /// </summary>
    public string? LoopCondition { get; init; }

    /// <summary>
    /// 循环体内的起始节点
    /// </summary>
    public required string BodyStartNodeId { get; init; }

    /// <summary>
    /// 循环变量名
    /// </summary>
    public string LoopVariable { get; init; } = "item";
}

/// <summary>
/// 循环类型
/// </summary>
public enum LoopType
{
    /// <summary>
    /// 固定次数循环
    /// </summary>
    Count,

    /// <summary>
    /// 条件循环
    /// </summary>
    While,

    /// <summary>
    /// 遍历集合
    /// </summary>
    ForEach,
}

/// <summary>
/// 并行分支
/// </summary>
public record ParallelBranch
{
    /// <summary>
    /// 分支名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 分支起始节点 ID
    /// </summary>
    public required string StartNodeId { get; init; }
}

/// <summary>
/// 并行节点配置
/// </summary>
public record ParallelNodeConfig
{
    /// <summary>
    /// 并行分支
    /// </summary>
    public List<ParallelBranch> Branches { get; init; } = [];

    /// <summary>
    /// 等待策略
    /// </summary>
    public ParallelWaitStrategy WaitStrategy { get; init; } = ParallelWaitStrategy.WaitAll;

    /// <summary>
    /// 结果合并策略
    /// </summary>
    public ParallelMergeStrategy MergeStrategy { get; init; } = ParallelMergeStrategy.Concatenate;

    /// <summary>
    /// 单个分支超时（毫秒）
    /// </summary>
    public int? BranchTimeoutMs { get; init; }
}

/// <summary>
/// 并行等待策略
/// </summary>
public enum ParallelWaitStrategy
{
    /// <summary>
    /// 等待所有完成
    /// </summary>
    WaitAll,

    /// <summary>
    /// 任意一个完成即可
    /// </summary>
    WaitAny,

    /// <summary>
    /// 等待指定数量完成
    /// </summary>
    WaitN,
}

/// <summary>
/// 并行结果合并策略
/// </summary>
public enum ParallelMergeStrategy
{
    /// <summary>
    /// 拼接所有结果
    /// </summary>
    Concatenate,

    /// <summary>
    /// 只取第一个结果
    /// </summary>
    First,

    /// <summary>
    /// 取最后一个结果
    /// </summary>
    Last,

    /// <summary>
    /// 自定义合并
    /// </summary>
    Custom,
}

/// <summary>
/// 子工作流节点配置
/// </summary>
public record SubWorkflowNodeConfig
{
    /// <summary>
    /// 子工作流 ID
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// 输入映射（父上下文键 -> 子上下文键）
    /// </summary>
    public Dictionary<string, string>? InputMapping { get; init; }

    /// <summary>
    /// 输出映射（子上下文键 -> 父上下文键）
    /// </summary>
    public Dictionary<string, string>? OutputMapping { get; init; }
}

/// <summary>
/// 人工审批节点配置
/// </summary>
public record HumanApprovalNodeConfig
{
    /// <summary>
    /// 审批提示信息
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// 批准后的下一个节点
    /// </summary>
    public required string ApprovedNodeId { get; init; }

    /// <summary>
    /// 拒绝后的下一个节点
    /// </summary>
    public string? RejectedNodeId { get; init; }

    /// <summary>
    /// 超时后的下一个节点
    /// </summary>
    public string? TimeoutNodeId { get; init; }
}

/// <summary>
/// 延迟节点配置
/// </summary>
public record DelayNodeConfig
{
    /// <summary>
    /// 延迟时间（毫秒）
    /// </summary>
    public int DelayMs { get; init; } = 1000;
}
