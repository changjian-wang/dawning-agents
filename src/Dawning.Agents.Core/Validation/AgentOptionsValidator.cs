using Dawning.Agents.Abstractions.Agent;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Agent 配置选项验证器
/// </summary>
public class AgentOptionsValidator : AbstractValidator<AgentOptions>
{
    public AgentOptionsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Agent 名称不能为空")
            .MaximumLength(100)
            .WithMessage("Agent 名称不能超过 100 个字符");

        RuleFor(x => x.Instructions)
            .NotEmpty()
            .WithMessage("Agent 指令不能为空")
            .MaximumLength(10000)
            .WithMessage("Agent 指令不能超过 10000 个字符");

        RuleFor(x => x.MaxSteps)
            .GreaterThan(0)
            .WithMessage("MaxSteps 必须大于 0")
            .LessThanOrEqualTo(100)
            .WithMessage("MaxSteps 不能超过 100");
    }
}
