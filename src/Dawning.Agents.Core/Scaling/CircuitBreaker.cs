namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 熔断器实现
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger<CircuitBreaker> _logger;

    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    public CircuitBreaker(
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null,
        ILogger<CircuitBreaker>? logger = null
    )
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger ?? NullLogger<CircuitBreaker>.Instance;
    }

    /// <inheritdoc />
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (
                    _state == CircuitState.Open
                    && DateTime.UtcNow - _lastFailureTime > _resetTimeout
                )
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation("熔断器转换为半开状态");
                }
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
        if (State == CircuitState.Open)
        {
            _logger.LogWarning("熔断器处于打开状态，拒绝请求");
            throw new CircuitBreakerOpenException("熔断器处于打开状态");
        }

        try
        {
            var result = await action();
            OnSuccess();
            return result;
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
                await action();
                return true;
            },
            cancellationToken
        );
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

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
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
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

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
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message)
        : base(message) { }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException) { }
}
