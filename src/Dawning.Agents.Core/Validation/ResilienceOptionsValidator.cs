using Dawning.Agents.Abstractions.Resilience;
using FluentValidation;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Validator for <see cref="ResilienceOptions"/>.
/// </summary>
public class ResilienceOptionsValidator : AbstractValidator<ResilienceOptions>
{
    public ResilienceOptionsValidator()
    {
        RuleFor(x => x.Retry).SetValidator(new RetryOptionsValidator());
        RuleFor(x => x.CircuitBreaker).SetValidator(new CircuitBreakerOptionsValidator());
        RuleFor(x => x.Timeout).SetValidator(new TimeoutOptionsValidator());
        RuleFor(x => x.Bulkhead).SetValidator(new BulkheadOptionsValidator());
    }
}

/// <summary>
/// Validator for <see cref="RetryOptions"/>.
/// </summary>
public class RetryOptionsValidator : AbstractValidator<RetryOptions>
{
    public RetryOptionsValidator()
    {
        RuleFor(x => x.MaxRetryAttempts)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxRetryAttempts must not be negative.")
            .LessThanOrEqualTo(10)
            .WithMessage("MaxRetryAttempts must not exceed 10.");

        RuleFor(x => x.BaseDelayMs)
            .GreaterThanOrEqualTo(0)
            .WithMessage("BaseDelayMs must not be negative.")
            .LessThanOrEqualTo(60000)
            .WithMessage("BaseDelayMs must not exceed 60000.");

        RuleFor(x => x.MaxDelayMs)
            .GreaterThan(0)
            .WithMessage("MaxDelayMs must be greater than 0.")
            .GreaterThanOrEqualTo(x => x.BaseDelayMs)
            .WithMessage("MaxDelayMs must be greater than or equal to BaseDelayMs.");
    }
}

/// <summary>
/// Validator for <see cref="CircuitBreakerOptions"/>.
/// </summary>
public class CircuitBreakerOptionsValidator : AbstractValidator<CircuitBreakerOptions>
{
    public CircuitBreakerOptionsValidator()
    {
        RuleFor(x => x.FailureRatio)
            .GreaterThan(0.0)
            .WithMessage("FailureRatio must be greater than 0.")
            .LessThanOrEqualTo(1.0)
            .WithMessage("FailureRatio must not exceed 1.0.");

        RuleFor(x => x.SamplingDurationSeconds)
            .GreaterThan(0)
            .WithMessage("SamplingDurationSeconds must be greater than 0.");

        RuleFor(x => x.MinimumThroughput)
            .GreaterThan(0)
            .WithMessage("MinimumThroughput must be greater than 0.");

        RuleFor(x => x.BreakDurationSeconds)
            .GreaterThan(0)
            .WithMessage("BreakDurationSeconds must be greater than 0.");
    }
}

/// <summary>
/// Validator for <see cref="TimeoutOptions"/>.
/// </summary>
public class TimeoutOptionsValidator : AbstractValidator<TimeoutOptions>
{
    public TimeoutOptionsValidator()
    {
        RuleFor(x => x.TimeoutSeconds)
            .GreaterThan(0)
            .WithMessage("TimeoutSeconds must be greater than 0.")
            .LessThanOrEqualTo(600)
            .WithMessage("TimeoutSeconds must not exceed 600.");
    }
}

/// <summary>
/// Validator for <see cref="BulkheadOptions"/>.
/// </summary>
public class BulkheadOptionsValidator : AbstractValidator<BulkheadOptions>
{
    public BulkheadOptionsValidator()
    {
        RuleFor(x => x.MaxConcurrency)
            .GreaterThan(0)
            .WithMessage("MaxConcurrency must be greater than 0.")
            .LessThanOrEqualTo(1000)
            .WithMessage("MaxConcurrency must not exceed 1000.");

        RuleFor(x => x.MaxQueuedActions)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MaxQueuedActions must not be negative.")
            .LessThanOrEqualTo(10000)
            .WithMessage("MaxQueuedActions must not exceed 10000.");
    }
}
