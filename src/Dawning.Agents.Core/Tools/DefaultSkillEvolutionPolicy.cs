using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// Default skill evolution policy — makes promotion/retirement decisions based on success rate and call count.
/// </summary>
public sealed class DefaultSkillEvolutionPolicy : ISkillEvolutionPolicy
{
    private readonly ILogger<DefaultSkillEvolutionPolicy> _logger;

    /// <summary>
    /// Creates the default skill evolution policy.
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

        // Success rate ≥ 90% and calls ≥ 10 → Global
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

        // Success rate ≥ 80% and calls ≥ 3 → User
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

        // Success rate < 20% and calls ≥ 5 → retire
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
