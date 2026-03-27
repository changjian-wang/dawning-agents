using Dawning.Agents.Abstractions.Orchestration;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="OrchestratorOptions"/>.
/// </summary>
public class OrchestratorOptionsValidator : AbstractValidator<OrchestratorOptions>
{
    public OrchestratorOptionsValidator()
    {
        RuleFor(x => x.MaxConcurrency)
            .GreaterThan(0)
            .WithMessage("MaxConcurrency must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("MaxConcurrency must not exceed 100.");

        RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("TimeoutSeconds must be greater than 0.")
            .LessThanOrEqualTo(3600)
            .WithMessage("TimeoutSeconds must not exceed 3600 (1 hour).");

        RuleFor(x => x.AgentTimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("AgentTimeoutSeconds must be greater than 0.")
            .LessThanOrEqualTo(x => x.TimeoutSeconds)
            .WithMessage("AgentTimeoutSeconds must not exceed TimeoutSeconds.");

        RuleFor(x => x.AggregationStrategy)
            .IsInEnum()
            .WithMessage("AggregationStrategy must be a valid aggregation strategy.");
    }
}
