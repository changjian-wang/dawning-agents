using Dawning.Agents.Abstractions.HumanLoop;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// 人机协作配置选项验证器
/// </summary>
public class HumanLoopOptionsValidator : AbstractValidator<HumanLoopOptions>
{
    public HumanLoopOptionsValidator()
    {
        RuleFor(x => x.DefaultTimeout)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("DefaultTimeout 必须大于 0")
            .LessThanOrEqualTo(TimeSpan.FromHours(24))
            .WithMessage("DefaultTimeout 不能超过 24 小时");

        RuleFor(x => x.MaxRetries)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRetries 不能为负数")
            .LessThanOrEqualTo(10)
            .WithMessage("MaxRetries 不能超过 10");

        RuleFor(x => x.HighRiskKeywords)
            .NotNull()
            .WithMessage("HighRiskKeywords 不能为 null");

        RuleFor(x => x.CriticalRiskKeywords)
            .NotNull()
            .WithMessage("CriticalRiskKeywords 不能为 null");
    }
}

/// <summary>
/// 审批配置验证器
/// </summary>
public class ApprovalConfigValidator : AbstractValidator<ApprovalConfig>
{
    private static readonly string[] ValidTimeoutActions = ["approve", "reject"];

    public ApprovalConfigValidator()
    {
        RuleFor(x => x.ApprovalTimeout)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("ApprovalTimeout 必须大于 0")
            .LessThanOrEqualTo(TimeSpan.FromHours(72))
            .WithMessage("ApprovalTimeout 不能超过 72 小时");

        RuleFor(x => x.DefaultOnTimeout)
            .NotEmpty()
            .WithMessage("DefaultOnTimeout 不能为空")
            .Must(action => ValidTimeoutActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            .WithMessage("DefaultOnTimeout 必须是 'approve' 或 'reject'");
    }
}
