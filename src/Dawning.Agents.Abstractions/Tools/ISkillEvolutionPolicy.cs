namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 提升决策
/// </summary>
/// <param name="ShouldPromote">是否应提升</param>
/// <param name="TargetScope">目标范围</param>
/// <param name="Reason">决策原因</param>
public record PromotionDecision(bool ShouldPromote, ToolScope TargetScope, string Reason);

/// <summary>
/// 技能演化策略 — 决定技能的提升、淘汰和归档
/// </summary>
public interface ISkillEvolutionPolicy
{
    /// <summary>
    /// 评估 session 工具是否应提升到持久化层级
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="stats">效用统计</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<PromotionDecision> EvaluatePromotionAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 评估工具是否应淘汰
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="stats">效用统计</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ShouldRetireAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    );
}
