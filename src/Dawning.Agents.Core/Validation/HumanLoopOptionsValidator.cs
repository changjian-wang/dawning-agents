using Dawning.Agents.Abstractions.HumanLoop;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="HumanLoopOptions"/>.
/// </summary>
public class HumanLoopOptionsValidator : AbstractValidator<HumanLoopOptions>
{
    public HumanLoopOptionsValidator()
    {
        RuleFor(x => x.DefaultTimeout)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("DefaultTimeout must be greater than zero.")
            .LessThanOrEqualTo(TimeSpan.FromHours(24))
            .WithMessage("DefaultTimeout must not exceed 24 hours.");

        RuleFor(x => x.MaxRetries)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRetries must not be negative.")
            .LessThanOrEqualTo(10)
            .WithMessage("MaxRetries must not exceed 10.");

        RuleFor(x => x.HighRiskKeywords).NotNull().WithMessage("HighRiskKeywords must not be null.");

        RuleFor(x => x.CriticalRiskKeywords)
            .NotNull()
            .WithMessage("CriticalRiskKeywords must not be null.");
    }
}

/// <summary>
/// Validator for <see cref="ApprovalConfig"/>.
/// </summary>
public class ApprovalConfigValidator : AbstractValidator<ApprovalConfig>
{
    private static readonly string[] s_validTimeoutActions = ["approve", "reject"];

    public ApprovalConfigValidator()
    {
        RuleFor(x => x.ApprovalTimeout)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("ApprovalTimeout must be greater than zero.")
            .LessThanOrEqualTo(TimeSpan.FromHours(72))
            .WithMessage("ApprovalTimeout must not exceed 72 hours.");

        RuleFor(x => x.DefaultOnTimeout)
            .NotEmpty()
            .WithMessage("DefaultOnTimeout must not be empty.")
            .Must(action =>
                s_validTimeoutActions.Contains(action, StringComparer.OrdinalIgnoreCase)
            )
            .WithMessage("DefaultOnTimeout must be 'approve' or 'reject'.");
    }
}
