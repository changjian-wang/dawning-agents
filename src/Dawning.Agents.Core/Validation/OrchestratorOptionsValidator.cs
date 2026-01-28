using Dawning.Agents.Abstractions.Orchestration;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// 编排器配置选项验证器
/// </summary>
public class OrchestratorOptionsValidator : AbstractValidator<OrchestratorOptions>
{
    public OrchestratorOptionsValidator()
    {
        RuleFor(x => x.MaxConcurrency)
            .GreaterThan(0)
            .WithMessage("MaxConcurrency 必须大于 0")
            .LessThanOrEqualTo(100)
            .WithMessage("MaxConcurrency 不能超过 100");

        RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("TimeoutSeconds 必须大于 0")
            .LessThanOrEqualTo(3600)
            .WithMessage("TimeoutSeconds 不能超过 3600（1小时）");

        RuleFor(x => x.AgentTimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("AgentTimeoutSeconds 必须大于 0")
            .LessThanOrEqualTo(x => x.TimeoutSeconds)
            .WithMessage("AgentTimeoutSeconds 不能超过 TimeoutSeconds");

        RuleFor(x => x.AggregationStrategy)
            .IsInEnum()
            .WithMessage("AggregationStrategy 必须是有效的聚合策略");
    }
}
