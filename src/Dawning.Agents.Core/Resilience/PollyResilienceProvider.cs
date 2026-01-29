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
/// 基于 Polly V8 的弹性策略提供者实现
/// </summary>
/// <remarks>
/// 支持的策略（按执行顺序）：
/// 1. 超时 (Timeout) - 最外层，限制总执行时间
/// 2. 舱壁隔离 (Bulkhead) - 限制并发数
/// 3. 重试 (Retry) - 失败后重试
/// 4. 断路器 (CircuitBreaker) - 防止级联故障
/// </remarks>
public class PollyResilienceProvider : IResilienceProvider
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<PollyResilienceProvider> _logger;

    public PollyResilienceProvider(
        IOptions<ResilienceOptions> options,
        ILogger<PollyResilienceProvider>? logger = null
    )
    {
        _logger = logger ?? NullLogger<PollyResilienceProvider>.Instance;

        var resilienceOptions = options.Value;
        _pipeline = BuildPipeline(resilienceOptions);

        _logger.LogDebug("PollyResilienceProvider 已创建");
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    )
    {
        return await _pipeline.ExecuteAsync(
            async token => await operation(token),
            cancellationToken
        );
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
    {
        await _pipeline.ExecuteAsync(
            async token =>
            {
                await operation(token);
            },
            cancellationToken
        );
    }

    private ResiliencePipeline BuildPipeline(ResilienceOptions options)
    {
        var builder = new ResiliencePipelineBuilder();

        // 1. 超时策略（最外层）
        if (options.Timeout.Enabled)
        {
            builder.AddTimeout(
                new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(options.Timeout.TimeoutSeconds),
                    OnTimeout = args =>
                    {
                        _logger.LogWarning(
                            "请求超时，超时时间: {Timeout}s",
                            options.Timeout.TimeoutSeconds
                        );
                        return default;
                    },
                }
            );
        }

        // 2. 舱壁隔离策略（并发限制）
        if (options.Bulkhead.Enabled)
        {
            var limiter = new ConcurrencyLimiter(
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
                    RateLimiter = args => limiter.AcquireAsync(cancellationToken: args.Context.CancellationToken),
                    OnRejected = args =>
                    {
                        _logger.LogWarning(
                            "舱壁隔离：并发请求过多，已拒绝请求。当前限制: {MaxConcurrency}",
                            options.Bulkhead.MaxConcurrency
                        );
                        return default;
                    },
                }
            );

            _logger.LogDebug(
                "舱壁隔离已启用，最大并发: {MaxConcurrency}，最大排队: {MaxQueued}",
                options.Bulkhead.MaxConcurrency,
                options.Bulkhead.MaxQueuedActions
            );
        }

        // 3. 重试策略
        if (options.Retry.Enabled)
        {
            builder.AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    BackoffType = options.Retry.UseJitter
                        ? DelayBackoffType.Exponential
                        : DelayBackoffType.Constant,
                    Delay = TimeSpan.FromMilliseconds(options.Retry.BaseDelayMs),
                    MaxDelay = TimeSpan.FromMilliseconds(options.Retry.MaxDelayMs),
                    UseJitter = options.Retry.UseJitter,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .Handle<TimeoutRejectedException>(),
                    OnRetry = args =>
                    {
                        _logger.LogWarning(
                            "请求失败，第 {AttemptNumber} 次重试，延迟 {RetryDelay}ms，异常: {Exception}",
                            args.AttemptNumber,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? "Unknown"
                        );
                        return default;
                    },
                }
            );
        }

        // 4. 断路器策略（最内层）
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
                        .Handle<TaskCanceledException>(),
                    OnOpened = args =>
                    {
                        _logger.LogError(
                            "断路器打开，断路时间: {BreakDuration}s，异常: {Exception}",
                            options.CircuitBreaker.BreakDurationSeconds,
                            args.Outcome.Exception?.Message ?? "Unknown"
                        );
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        _logger.LogInformation("断路器关闭，服务恢复正常");
                        return default;
                    },
                    OnHalfOpened = _ =>
                    {
                        _logger.LogInformation("断路器半开，尝试恢复");
                        return default;
                    },
                }
            );
        }

        return builder.Build();
    }
}
