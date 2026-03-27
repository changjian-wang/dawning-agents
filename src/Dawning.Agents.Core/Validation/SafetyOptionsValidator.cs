using Dawning.Agents.Abstractions.Safety;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="SafetyOptions"/>.
/// </summary>
public class SafetyOptionsValidator : AbstractValidator<SafetyOptions>
{
    public SafetyOptionsValidator()
    {
        RuleFor(x => x.MaxInputLength)
            .GreaterThan(0)
            .WithMessage("MaxInputLength must be greater than 0.")
            .LessThanOrEqualTo(1000000)
            .WithMessage("MaxInputLength must not exceed 1000000.");

        RuleFor(x => x.MaxOutputLength)
            .GreaterThan(0)
            .WithMessage("MaxOutputLength must be greater than 0.")
            .LessThanOrEqualTo(10000000)
            .WithMessage("MaxOutputLength must not exceed 10000000.");

        RuleForEach(x => x.SensitivePatterns).SetValidator(new SensitivePatternValidator());
    }
}

/// <summary>
/// Validator for <see cref="SensitivePattern"/>.
/// </summary>
public class SensitivePatternValidator : AbstractValidator<SensitivePattern>
{
    public SensitivePatternValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Pattern name must not be empty.");

        RuleFor(x => x.Pattern)
            .NotEmpty()
            .WithMessage("Regex pattern must not be empty.")
            .Must(BeValidRegex)
            .WithMessage("Regex pattern format is invalid.");

        RuleFor(x => x.KeepFirst).GreaterThanOrEqualTo(0).WithMessage("KeepFirst must not be negative.");

        RuleFor(x => x.KeepLast).GreaterThanOrEqualTo(0).WithMessage("KeepLast must not be negative.");
    }

    private static bool BeValidRegex(string pattern)
    {
        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
