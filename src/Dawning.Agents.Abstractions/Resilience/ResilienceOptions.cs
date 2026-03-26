using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Resilience;

/// <summary>
/// Resilience strategy configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
/// <code>
/// {
///   "Resilience": {
///     "Retry": {
///       "MaxRetryAttempts": 3,
///       "BaseDelayMs": 1000,
///       "UseJitter": true
///     },
///     "CircuitBreaker": {
///       "FailureRatio": 0.5,
///       "SamplingDurationSeconds": 30,
///       "MinimumThroughput": 10,
///       "BreakDurationSeconds": 30
///     },
///     "Timeout": {
///       "TimeoutSeconds": 120
///     }
///   }
/// }
/// </code>
/// </remarks>
public class ResilienceOptions : IValidatableOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Resilience";

    /// <summary>
    /// Retry strategy configuration.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Circuit breaker strategy configuration.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Timeout strategy configuration.
    /// </summary>
    public TimeoutOptions Timeout { get; set; } = new();

    /// <summary>
    /// Bulkhead isolation strategy configuration.
    /// </summary>
    public BulkheadOptions Bulkhead { get; set; } = new();

    /// <inheritdoc />
    public void Validate()
    {
        Retry.Validate();
        CircuitBreaker.Validate();
        Timeout.Validate();
        Bulkhead.Validate();
    }
}

/// <summary>
/// Retry strategy configuration.
/// </summary>
public class RetryOptions : IValidatableOptions
{
    /// <summary>
    /// Whether retry is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay time in milliseconds.
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to use jitter to avoid the thundering herd effect.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Backoff strategy type (Constant=fixed delay, Exponential=exponential backoff, Linear=linear increase).
    /// </summary>
    public RetryBackoffType BackoffType { get; set; } = RetryBackoffType.Constant;

    /// <summary>
    /// Maximum delay time in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("RetryOptions.MaxRetryAttempts must be >= 0.");
        }

        if (BaseDelayMs < 0)
        {
            throw new InvalidOperationException("RetryOptions.BaseDelayMs must be >= 0.");
        }

        if (MaxDelayMs < BaseDelayMs)
        {
            throw new InvalidOperationException("RetryOptions.MaxDelayMs must be >= BaseDelayMs.");
        }
    }
}

/// <summary>
/// Circuit breaker strategy configuration.
/// </summary>
public class CircuitBreakerOptions : IValidatableOptions
{
    /// <summary>
    /// Whether the circuit breaker is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Failure ratio to trigger the circuit breaker (0.0-1.0).
    /// </summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Sampling time window in seconds.
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum request count (circuit breaker will not trigger below this threshold).
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Circuit break duration in seconds.
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;

    /// <inheritdoc />
    public void Validate()
    {
        if (FailureRatio is < 0.0 or > 1.0)
        {
            throw new InvalidOperationException(
                "CircuitBreakerOptions.FailureRatio must be between 0.0 and 1.0."
            );
        }

        if (SamplingDurationSeconds <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreakerOptions.SamplingDurationSeconds must be > 0."
            );
        }

        if (MinimumThroughput <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreakerOptions.MinimumThroughput must be > 0."
            );
        }

        if (BreakDurationSeconds <= 0)
        {
            throw new InvalidOperationException(
                "CircuitBreakerOptions.BreakDurationSeconds must be > 0."
            );
        }
    }
}

/// <summary>
/// Timeout strategy configuration.
/// </summary>
public class TimeoutOptions : IValidatableOptions
{
    /// <summary>
    /// Whether timeout is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Timeout duration in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <inheritdoc />
    public void Validate()
    {
        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("TimeoutOptions.TimeoutSeconds must be > 0.");
        }
    }
}

/// <summary>
/// Bulkhead isolation strategy configuration.
/// </summary>
/// <remarks>
/// Bulkhead isolation limits the number of concurrent requests to prevent resource exhaustion.
/// </remarks>
public class BulkheadOptions : IValidatableOptions
{
    /// <summary>
    /// Whether bulkhead isolation is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum concurrent executions.
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// Maximum queued actions.
    /// </summary>
    public int MaxQueuedActions { get; set; } = 20;

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxConcurrency <= 0)
        {
            throw new InvalidOperationException("BulkheadOptions.MaxConcurrency must be > 0.");
        }

        if (MaxQueuedActions < 0)
        {
            throw new InvalidOperationException("BulkheadOptions.MaxQueuedActions must be >= 0.");
        }
    }
}

/// <summary>
/// Retry backoff strategy type.
/// </summary>
public enum RetryBackoffType
{
    /// <summary>Fixed delay.</summary>
    Constant = 0,

    /// <summary>Linear increase.</summary>
    Linear = 1,

    /// <summary>Exponential backoff.</summary>
    Exponential = 2,
}
