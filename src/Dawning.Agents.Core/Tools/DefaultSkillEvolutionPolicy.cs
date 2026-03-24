using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 默认技能演化策略 — 基于成功率和调用次数做提升/淘汰决策
/// </summary>
public sealed class DefaultSkillEvolutionPolicy : ISkillEvolutionPolicy
{
    private readonly ILogger<DefaultSkillEvolutionPolicy> _logger;

    /// <summary>
    /// 创建默认技能演化策略
    /// </summary>
    public DefaultSkillEvolutionPolicy(ILogger<DefaultSkillEvolutionPolicy>? logger = null)
    {
        _logger = logger ?? NullLogger<DefaultSkillEvolutionPolicy>.Instance;
    }

    /// <inheritdoc />
    public Task<PromotionDecision> EvaluatePromotionAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(stats);

        // 成功率 ≥ 90% 且调用 ≥ 10 次 → Global
        if (stats.SuccessRate >= 0.9f && stats.TotalCalls >= 10)
        {
            _logger.LogInformation(
                "Tool '{ToolName}' qualifies for Global promotion: {SuccessRate:P0} success, {Calls} calls",
                toolName,
                stats.SuccessRate,
                stats.TotalCalls
            );

            return Task.FromResult(
                new PromotionDecision(
                    true,
                    ToolScope.Global,
                    $"High success rate ({stats.SuccessRate:P0}) with {stats.TotalCalls} calls"
                )
            );
        }

        // 成功率 ≥ 80% 且调用 ≥ 3 次 → User
        if (stats.SuccessRate >= 0.8f && stats.TotalCalls >= 3)
        {
            _logger.LogInformation(
                "Tool '{ToolName}' qualifies for User promotion: {SuccessRate:P0} success, {Calls} calls",
                toolName,
                stats.SuccessRate,
                stats.TotalCalls
            );

            return Task.FromResult(
                new PromotionDecision(
                    true,
                    ToolScope.User,
                    $"Good success rate ({stats.SuccessRate:P0}) with {stats.TotalCalls} calls"
                )
            );
        }

        return Task.FromResult(
            new PromotionDecision(false, ToolScope.Session, "Does not meet promotion criteria")
        );
    }

    /// <inheritdoc />
    public Task<bool> ShouldRetireAsync(
        string toolName,
        ToolUsageStats stats,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(stats);

        // 成功率 < 20% 且调用 ≥ 5 次 → 淘汰
        if (stats.SuccessRate < 0.2f && stats.TotalCalls >= 5)
        {
            _logger.LogWarning(
                "Tool '{ToolName}' recommended for retirement: {SuccessRate:P0} success, {Calls} calls",
                toolName,
                stats.SuccessRate,
                stats.TotalCalls
            );

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
