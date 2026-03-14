namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 熔断器实现
/// </summary>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly TimeProvider _timeProvider;

    private int _failureCount;
    private DateTimeOffset _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;
    private bool _halfOpenTrialActive;
    private readonly Lock _lock = new();

    public CircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null,
        ILogger<CircuitBreaker>? logger = null,
        TimeProvider? timeProvider = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
        if (resetTimeout.HasValue && resetTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetTimeout),
                "resetTimeout must be greater than zero."
            );
        }

        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger ?? NullLogger<CircuitBreaker>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public int FailureCount
    {
        get
        {
            lock (_lock)
            {
                return _failureCount;
            }
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken = default
    )
    {
        var currentState = GetCurrentState();
        if (currentState == CircuitState.Open)
        {
            _logger.LogWarning("熔断器处于打开状态，拒绝请求");
            throw new CircuitBreakerOpenException("熔断器处于打开状态");
        }

        // HalfOpen 状态只允许一个试探请求通过，防止惊群效应
        if (currentState == CircuitState.HalfOpen)
        {
            lock (_lock)
            {
                if (_halfOpenTrialActive)
                {
                    throw new CircuitBreakerOpenException("熔断器处于半开状态，试探请求进行中");
                }
                _halfOpenTrialActive = true;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await action().ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch (OperationCanceledException)
        {
            // 取消操作不计入失败，但必须释放 HalfOpen 试探锁
            // 否则 _halfOpenTrialActive 永远为 true，熔断器永久卡死
            lock (_lock)
            {
                _halfOpenTrialActive = false;
            }
            throw;
        }
        catch (Exception ex) when (ex is not CircuitBreakerOpenException)
        {
            OnFailure();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
                async () =>
                {
                    await action().ConfigureAwait(false);
                    return true;
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
            _logger.LogInformation("熔断器已重置");
        }
    }

    /// <summary>
    /// 获取当前状态（检查是否需要从 Open 转换为 HalfOpen）
    /// </summary>
    private CircuitState GetCurrentState()
    {
        lock (_lock)
        {
            if (
                _state == CircuitState.Open
                && _timeProvider.GetUtcNow() - _lastFailureTime > _resetTimeout
            )
            {
                _state = CircuitState.HalfOpen;
                _halfOpenTrialActive = false;
                _logger.LogInformation("熔断器转换为半开状态");
            }
            return _state;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _halfOpenTrialActive = false;
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _logger.LogInformation("熔断器在成功请求后关闭");
            }
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _halfOpenTrialActive = false;
            _failureCount++;
            _lastFailureTime = _timeProvider.GetUtcNow();

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                _logger.LogWarning("熔断器在 {FailureCount} 次失败后打开", _failureCount);
            }
        }
    }
}

/// <summary>
/// 熔断器打开异常
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException() { }

    public CircuitBreakerOpenException(string message)
        : base(message) { }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException) { }
}
