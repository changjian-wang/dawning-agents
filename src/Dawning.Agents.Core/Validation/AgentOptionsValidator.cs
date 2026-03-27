using Dawning.Agents.Abstractions.Agent;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="AgentOptions"/>.
/// </summary>
public class AgentOptionsValidator : AbstractValidator<AgentOptions>
{
    public AgentOptionsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Agent name must not be empty.")
            .MaximumLength(100)
            .WithMessage("Agent name must not exceed 100 characters.");

        RuleFor(x => x.Instructions)
            .NotEmpty()
            .WithMessage("Agent instructions must not be empty.")
            .MaximumLength(10000)
            .WithMessage("Agent instructions must not exceed 10000 characters.");

        RuleFor(x => x.MaxSteps)
            .GreaterThan(0)
            .WithMessage("MaxSteps must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("MaxSteps must not exceed 100.");

        RuleFor(x => x.MaxTokens).GreaterThan(0).WithMessage("MaxTokens must be greater than 0.");

        RuleFor(x => x.MaxCostPerRun)
            .GreaterThan(0m)
            .When(x => x.MaxCostPerRun.HasValue)
            .WithMessage("MaxCostPerRun must be greater than 0.");
    }
}
