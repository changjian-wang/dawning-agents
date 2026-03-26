namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Promotion decision.
/// </summary>
/// <param name="ShouldPromote">Whether the tool should be promoted.</param>
/// <param name="TargetScope">Target scope.</param>
/// <param name="Reason">Decision reason.</param>
public record PromotionDecision(bool ShouldPromote, ToolScope TargetScope, string Reason);

/// <summary>
/// Skill evolution policy — determines skill promotion, retirement, and archival.
/// </summary>
public interface ISkillEvolutionPolicy
{
    /// <summary>
    /// Evaluates whether a session tool should be promoted to a persistent level.
    /// </summary>
    /// <param name="toolName">Tool name.</param>
    /// <param name="stats">Utility statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PromotionDecision> EvaluatePromotionAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates whether a tool should be retired.
    /// </summary>
    /// <param name="toolName">Tool name.</param>
    /// <param name="stats">Utility statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ShouldRetireAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    );
}
