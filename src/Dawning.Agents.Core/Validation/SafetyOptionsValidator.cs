using Dawning.Agents.Abstractions.Safety;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// 安全护栏配置选项验证器
/// </summary>
public class SafetyOptionsValidator : AbstractValidator<SafetyOptions>
{
    public SafetyOptionsValidator()
    {
        RuleFor(x => x.MaxInputLength)
            .GreaterThan(0)
            .WithMessage("MaxInputLength 必须大于 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("MaxInputLength 不能超过 1000000");

        RuleFor(x => x.MaxOutputLength)
            .GreaterThan(0)
            .WithMessage("MaxOutputLength 必须大于 0")
            .LessThanOrEqualTo(10000000)
            .WithMessage("MaxOutputLength 不能超过 10000000");

        RuleForEach(x => x.SensitivePatterns)
            .SetValidator(new SensitivePatternValidator());
    }
}

/// <summary>
/// 敏感数据模式验证器
/// </summary>
public class SensitivePatternValidator : AbstractValidator<SensitivePattern>
{
    public SensitivePatternValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("模式名称不能为空");

        RuleFor(x => x.Pattern)
            .NotEmpty()
            .WithMessage("正则表达式模式不能为空")
            .Must(BeValidRegex)
            .WithMessage("正则表达式模式格式无效");

        RuleFor(x => x.KeepFirst)
            .GreaterThanOrEqualTo(0)
            .WithMessage("KeepFirst 不能为负数");

        RuleFor(x => x.KeepLast)
            .GreaterThanOrEqualTo(0)
            .WithMessage("KeepLast 不能为负数");
    }

    private static bool BeValidRegex(string pattern)
    {
        try
        {
            _ = new System.Text.RegularExpressions.Regex(pattern);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
