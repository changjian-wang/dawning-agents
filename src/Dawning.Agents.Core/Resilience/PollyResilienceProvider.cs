using System.Threading.RateLimiting;
using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// Polly V8-based resilience provider implementation.
/// </summary>
/// <remarks>
/// Supported strategies (in execution order):
/// 1. Timeout - outermost layer, limits total execution time
/// 2. Bulkhead isolation - limits concurrency
/// 3. Retry - retries on failure
/// 4. Circuit breaker - prevents cascading failures
/// </remarks>
public sealed class PollyResilienceProvider : IResilienceProvider, IDisposable
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<PollyResilienceProvider> _logger;
    private ConcurrencyLimiter? _concurrencyLimiter;

    public PollyResilienceProvider(
        IOptions<ResilienceOptions> options,
        ILogger<PollyResilienceProvider>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? NullLogger<PollyResilienceProvider>.Instance;

        var resilienceOptions = options.Value;
        _pipeline = BuildPipeline(resilienceOptions);

        _logger.LogDebug("PollyResilienceProvider created");
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    )
    {
        return await _pipeline
            .ExecuteAsync(
                async token => await operation(token).ConfigureAwait(false),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
    {
        await _pipeline
            .ExecuteAsync(
                async token =>
                {
                    await operation(token).ConfigureAwait(false);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private ResiliencePipeline BuildPipeline(ResilienceOptions options)
    {
        var builder = new ResiliencePipelineBuilder();

        // 1. Timeout strategy (outermost layer)
        if (options.Timeout.Enabled)
        {
            builder.AddTimeout(
                new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(options.Timeout.TimeoutSeconds),
                    OnTimeout = args =>
                    {
                        _logger.LogWarning(
                            "Request timed out after {Timeout}s",
                            options.Timeout.TimeoutSeconds
                        );
                        return default;
                    },
                }
            );
        }

        // 2. Bulkhead isolation strategy (concurrency limiting)
        if (options.Bulkhead.Enabled)
        {
            _concurrencyLimiter = new ConcurrencyLimiter(
                new ConcurrencyLimiterOptions
                {
                    PermitLimit = options.Bulkhead.MaxConcurrency,
                    QueueLimit = options.Bulkhead.MaxQueuedActions,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }
            );

            builder.AddRateLimiter(
                new RateLimiterStrategyOptions
                {
                    RateLimiter = args =>
                        _concurrencyLimiter.AcquireAsync(
                            cancellationToken: args.Context.CancellationToken
                        ),
                    OnRejected = args =>
                    {
                        _logger.LogWarning(
                            "Bulkhead isolation: too many concurrent requests; request rejected. Current limit: {MaxConcurrency}",
                            options.Bulkhead.MaxConcurrency
                        );
                        return default;
                    },
                }
            );

            _logger.LogDebug(
                "Bulkhead isolation enabled, max concurrency: {MaxConcurrency}, max queued: {MaxQueued}",
                options.Bulkhead.MaxConcurrency,
                options.Bulkhead.MaxQueuedActions
            );
        }

        // 3. Retry strategy
        if (options.Retry.Enabled)
        {
            builder.AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    BackoffType = options.Retry.BackoffType switch
                    {
                        RetryBackoffType.Exponential => DelayBackoffType.Exponential,
                        RetryBackoffType.Linear => DelayBackoffType.Linear,
                        _ => DelayBackoffType.Constant,
                    },
                    Delay = TimeSpan.FromMilliseconds(options.Retry.BaseDelayMs),
                    MaxDelay = TimeSpan.FromMilliseconds(options.Retry.MaxDelayMs),
                    UseJitter = options.Retry.UseJitter,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>(),
                    OnRetry = args =>
                    {
                        _logger.LogWarning(
                            "Request failed, retry attempt {AttemptNumber}, delay {RetryDelay}ms, exception: {Exception}",
                            args.AttemptNumber,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? "Unknown"
                        );
                        return default;
                    },
                }
            );
        }

        // 4. Circuit breaker strategy (innermost layer)
        if (options.CircuitBreaker.Enabled)
        {
            builder.AddCircuitBreaker(
                new CircuitBreakerStrategyOptions
                {
                    FailureRatio = options.CircuitBreaker.FailureRatio,
                    SamplingDuration = TimeSpan.FromSeconds(
                        options.CircuitBreaker.SamplingDurationSeconds
                    ),
                    MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                    BreakDuration = TimeSpan.FromSeconds(
                        options.CircuitBreaker.BreakDurationSeconds
                    ),
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>(),
                    OnOpened = args =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened, break duration: {BreakDuration}s, exception: {Exception}",
                            options.CircuitBreaker.BreakDurationSeconds,
                            args.Outcome.Exception?.Message ?? "Unknown"
                        );
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        _logger.LogInformation("Circuit breaker closed; service restored to normal");
                        return default;
                    },
                    OnHalfOpened = _ =>
                    {
                        _logger.LogInformation("Circuit breaker half-opened; attempting recovery");
                        return default;
                    },
                }
            );
        }

        return builder.Build();
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        _concurrencyLimiter?.Dispose();
    }
}
