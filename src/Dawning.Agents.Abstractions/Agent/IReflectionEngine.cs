using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// 反思引擎 — 工具执行失败后的诊断与修复决策
/// </summary>
/// <remarks>
/// <para>灵感来源: Memento-Skills Read-Write Reflective Learning</para>
/// <para>失败不仅是重试信号，而是训练信号</para>
/// </remarks>
public interface IReflectionEngine
{
    /// <summary>
    /// 对失败的工具执行进行反思，产生修复策略
    /// </summary>
    /// <param name="context">反思上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ReflectionResult> ReflectAsync(
        ReflectionContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 反思上下文
/// </summary>
public record ReflectionContext
{
    /// <summary>
    /// 失败的工具
    /// </summary>
    public required ITool FailedTool { get; init; }

    /// <summary>
    /// 工具的输入参数
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// 失败的执行结果
    /// </summary>
    public required ToolResult FailedResult { get; init; }

    /// <summary>
    /// 原始任务描述
    /// </summary>
    public required string TaskDescription { get; init; }

    /// <summary>
    /// 之前的执行步骤
    /// </summary>
    public IReadOnlyList<AgentStep>? PreviousSteps { get; init; }

    /// <summary>
    /// 工具的效用统计
    /// </summary>
    public ToolUsageStats? UsageStats { get; init; }
}

/// <summary>
/// 反思结果
/// </summary>
public record ReflectionResult
{
    /// <summary>
    /// 建议的修复策略
    /// </summary>
    public required ReflectionAction Action { get; init; }

    /// <summary>
    /// 修订后的工具定义（当 Action = ReviseAndRetry 时）
    /// </summary>
    public EphemeralToolDefinition? RevisedDefinition { get; init; }

    /// <summary>
    /// 诊断报告
    /// </summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// 置信度 (0-1)
    /// </summary>
    public float Confidence { get; init; }
}

/// <summary>
/// 反思修复策略
/// </summary>
public enum ReflectionAction
{
    /// <summary>
    /// 简单重试（临时性错误，如网络超时）
    /// </summary>
    Retry,

    /// <summary>
    /// 修改工具定义后重试
    /// </summary>
    ReviseAndRetry,

    /// <summary>
    /// 放弃该工具，选择其他工具
    /// </summary>
    Abandon,

    /// <summary>
    /// 创建全新工具
    /// </summary>
    CreateNew,

    /// <summary>
    /// 升级给人类处理
    /// </summary>
    Escalate,
}
