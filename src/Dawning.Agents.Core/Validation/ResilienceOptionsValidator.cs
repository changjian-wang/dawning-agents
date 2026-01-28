using Dawning.Agents.Abstractions.Resilience;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// 弹性配置选项验证器
/// </summary>
public class ResilienceOptionsValidator : AbstractValidator<ResilienceOptions>
{
    public ResilienceOptionsValidator()
    {
        RuleFor(x => x.Retry).SetValidator(new RetryOptionsValidator());
        RuleFor(x => x.CircuitBreaker).SetValidator(new CircuitBreakerOptionsValidator());
        RuleFor(x => x.Timeout).SetValidator(new TimeoutOptionsValidator());
    }
}

/// <summary>
/// 重试配置验证器
/// </summary>
public class RetryOptionsValidator : AbstractValidator<RetryOptions>
{
    public RetryOptionsValidator()
    {
        RuleFor(x => x.MaxRetryAttempts)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRetryAttempts 不能为负数")
            .LessThanOrEqualTo(10)
            .WithMessage("MaxRetryAttempts 不能超过 10");

        RuleFor(x => x.BaseDelayMs)
            .GreaterThan(0)
            .WithMessage("BaseDelayMs 必须大于 0")
            .LessThanOrEqualTo(60000)
            .WithMessage("BaseDelayMs 不能超过 60000");

        RuleFor(x => x.MaxDelayMs)
            .GreaterThanOrEqualTo(x => x.BaseDelayMs)
            .WithMessage("MaxDelayMs 必须大于等于 BaseDelayMs");
    }
}

/// <summary>
/// 断路器配置验证器
/// </summary>
public class CircuitBreakerOptionsValidator : AbstractValidator<CircuitBreakerOptions>
{
    public CircuitBreakerOptionsValidator()
    {
        RuleFor(x => x.FailureRatio)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("FailureRatio 必须在 0.0 到 1.0 之间");

        RuleFor(x => x.SamplingDurationSeconds)
            .GreaterThan(0)
            .WithMessage("SamplingDurationSeconds 必须大于 0");

        RuleFor(x => x.MinimumThroughput)
            .GreaterThan(0)
            .WithMessage("MinimumThroughput 必须大于 0");

        RuleFor(x => x.BreakDurationSeconds)
            .GreaterThan(0)
            .WithMessage("BreakDurationSeconds 必须大于 0");
    }
}

/// <summary>
/// 超时配置验证器
/// </summary>
public class TimeoutOptionsValidator : AbstractValidator<TimeoutOptions>
{
    public TimeoutOptionsValidator()
    {
        RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("TimeoutSeconds 必须大于 0")
            .LessThanOrEqualTo(600)
            .WithMessage("TimeoutSeconds 不能超过 600");
    }
}
