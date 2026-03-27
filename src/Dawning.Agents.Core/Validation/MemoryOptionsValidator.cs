using Dawning.Agents.Abstractions.Memory;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="MemoryOptions"/>.
/// </summary>
public class MemoryOptionsValidator : AbstractValidator<MemoryOptions>
{
    public MemoryOptionsValidator()
    {
        RuleFor(x => x.WindowSize)
            .GreaterThan(0)
            .WithMessage("WindowSize must be greater than 0.")
            .LessThanOrEqualTo(100)
            .WithMessage("WindowSize must not exceed 100.");

        RuleFor(x => x.MaxRecentMessages)
            .GreaterThan(0)
            .WithMessage("MaxRecentMessages must be greater than 0.")
            .LessThanOrEqualTo(50)
            .WithMessage("MaxRecentMessages must not exceed 50.");

        RuleFor(x => x.SummaryThreshold)
            .GreaterThan(0)
            .WithMessage("SummaryThreshold must be greater than 0.")
            .GreaterThanOrEqualTo(x => x.MaxRecentMessages)
            .WithMessage("SummaryThreshold must be greater than or equal to MaxRecentMessages.");

        RuleFor(x => x.ModelName).NotEmpty().WithMessage("ModelName must not be empty.");

        RuleFor(x => x.MaxContextTokens)
            .GreaterThan(0)
            .WithMessage("MaxContextTokens must be greater than 0.")
            .LessThanOrEqualTo(200000)
            .WithMessage("MaxContextTokens must not exceed 200000.");

        RuleFor(x => x.DowngradeThreshold)
            .GreaterThan(0)
            .WithMessage("DowngradeThreshold must be greater than 0.");

        RuleFor(x => x.RetrieveTopK)
            .GreaterThan(0)
            .WithMessage("RetrieveTopK must be greater than 0.");

        RuleFor(x => x.MinRelevanceScore)
            .InclusiveBetween(0.0f, 1.0f)
            .WithMessage("MinRelevanceScore must be between 0.0 and 1.0.");
    }
}
