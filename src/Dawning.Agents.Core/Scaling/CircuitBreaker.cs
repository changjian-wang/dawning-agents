namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Circuit breaker implementation.
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
            _logger.LogWarning("Circuit breaker is open; rejecting request");
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        // HalfOpen state allows only one trial request to prevent thundering herd
        if (currentState == CircuitState.HalfOpen)
        {
            lock (_lock)
            {
                if (_halfOpenTrialActive)
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is half-open; trial request in progress");
                }
                _halfOpenTrialActive = true;
            }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action().ConfigureAwait(false);
            OnSuccess();
            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation does not count as failure, but must release the HalfOpen trial lock;
            // otherwise _halfOpenTrialActive stays true forever, permanently blocking the breaker
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
            _halfOpenTrialActive = false;
            _state = CircuitState.Closed;
            _logger.LogInformation("Circuit breaker has been reset");
        }
    }

    /// <summary>
    /// Gets the current state, checking whether to transition from Open to HalfOpen.
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
                _logger.LogInformation("Circuit breaker transitioned to half-open state");
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
                _logger.LogInformation("Circuit breaker closed after successful request");
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
                _logger.LogWarning("Circuit breaker opened after {FailureCount} failures", _failureCount);
            }
        }
    }
}

/// <summary>
/// Circuit breaker open exception.
/// </summary>
public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException() { }

    public CircuitBreakerOpenException(string message)
        : base(message) { }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException) { }
}
