using Dawning.Agents.Abstractions.Memory;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Memory 配置选项验证器
/// </summary>
public class MemoryOptionsValidator : AbstractValidator<MemoryOptions>
{
    public MemoryOptionsValidator()
    {
        RuleFor(x => x.WindowSize)
            .GreaterThan(0)
            .WithMessage("WindowSize 必须大于 0")
            .LessThanOrEqualTo(100)
            .WithMessage("WindowSize 不能超过 100");

        RuleFor(x => x.MaxRecentMessages)
            .GreaterThan(0)
            .WithMessage("MaxRecentMessages 必须大于 0")
            .LessThanOrEqualTo(50)
            .WithMessage("MaxRecentMessages 不能超过 50");

        RuleFor(x => x.SummaryThreshold)
            .GreaterThan(0)
            .WithMessage("SummaryThreshold 必须大于 0")
            .GreaterThanOrEqualTo(x => x.MaxRecentMessages)
            .WithMessage("SummaryThreshold 必须大于等于 MaxRecentMessages");

        RuleFor(x => x.ModelName)
            .NotEmpty()
            .WithMessage("ModelName 不能为空");

        RuleFor(x => x.MaxContextTokens)
            .GreaterThan(0)
            .WithMessage("MaxContextTokens 必须大于 0")
            .LessThanOrEqualTo(200000)
            .WithMessage("MaxContextTokens 不能超过 200000");
    }
}
