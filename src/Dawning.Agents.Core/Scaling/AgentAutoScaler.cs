namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent auto-scaler implementation.
/// </summary>
public sealed class AgentAutoScaler : IAgentAutoScaler, IDisposable
{
    private readonly ScalingOptions _options;
    private readonly Func<Task<ScalingMetrics>> _metricsProvider;
    private readonly Func<int, CancellationToken, Task> _scaleAction;
    private readonly ILogger<AgentAutoScaler> _logger;
    private readonly TimeProvider _timeProvider;

    private int _currentInstances;
    private DateTimeOffset? _lastScaleUp;
    private DateTimeOffset? _lastScaleDown;
    private readonly SemaphoreSlim _evaluateLock = new(1, 1);
    private readonly Lock _stateLock = new();
    private volatile bool _disposed;

    public AgentAutoScaler(
        ScalingOptions options,
        Func<Task<ScalingMetrics>> metricsProvider,
        Func<int, CancellationToken, Task> scaleAction,
        ILogger<AgentAutoScaler>? logger = null,
        TimeProvider? timeProvider = null
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metricsProvider =
            metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
        _scaleAction = scaleAction ?? throw new ArgumentNullException(nameof(scaleAction));
        _logger = logger ?? NullLogger<AgentAutoScaler>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _currentInstances = options.MinInstances;
    }

    /// <inheritdoc />
    public int CurrentInstances
    {
        get
        {
            lock (_stateLock)
            {
                return _currentInstances;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastScaleUpTime
    {
        get
        {
            lock (_stateLock)
            {
                return _lastScaleUp;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastScaleDownTime
    {
        get
        {
            lock (_stateLock)
            {
                return _lastScaleDown;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ScalingDecision> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _evaluateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var metrics = await _metricsProvider().ConfigureAwait(false);
            int currentSnapshot;
            int newCount;
            ScalingDecision decision;

            lock (_stateLock)
            {
                currentSnapshot = _currentInstances;
                decision = MakeScalingDecision(metrics, currentSnapshot);

                if (decision.Action == ScalingAction.None)
                {
                    _logger.LogDebug(
                        "Scaling evaluation: no action needed (CPU: {Cpu}%, Mem: {Mem}%, Queue: {Queue})",
                        metrics.CpuPercent,
                        metrics.MemoryPercent,
                        metrics.QueueLength
                    );
                    return decision;
                }

                newCount =
                    decision.Action == ScalingAction.ScaleUp
                        ? Math.Min(_currentInstances + decision.Delta, _options.MaxInstances)
                        : Math.Max(_currentInstances - decision.Delta, _options.MinInstances);

                if (newCount == _currentInstances)
                {
                    return decision;
                }
            }

            await ApplyScalingAsync(newCount, decision, cancellationToken).ConfigureAwait(false);

            return decision;
        }
        finally
        {
            try
            {
                _evaluateLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // Dispose() was called concurrently — safe to ignore
            }
        }
    }

    private ScalingDecision MakeScalingDecision(ScalingMetrics metrics, int currentInstances)
    {
        var now = _timeProvider.GetUtcNow();

        // Check if scale-up is needed
        if (ShouldScaleUp(metrics, currentInstances))
        {
            var cooldown = TimeSpan.FromSeconds(_options.ScaleUpCooldownSeconds);
            if (!_lastScaleUp.HasValue || now - _lastScaleUp.Value > cooldown)
            {
                var delta = CalculateScaleUpDelta(metrics, currentInstances);
                return ScalingDecision.ScaleUp(
                    delta,
                    $"CPU: {metrics.CpuPercent:F1}%, Memory: {metrics.MemoryPercent:F1}%, Queue: {metrics.QueueLength}"
                );
            }
            _logger.LogDebug("Scale-up cooldown active; skipping scale-up");
        }

        // Check if scale-down is possible
        if (ShouldScaleDown(metrics, currentInstances))
        {
            var cooldown = TimeSpan.FromSeconds(_options.ScaleDownCooldownSeconds);
            if (!_lastScaleDown.HasValue || now - _lastScaleDown.Value > cooldown)
            {
                return ScalingDecision.ScaleDown(
                    1,
                    $"Low utilization - CPU: {metrics.CpuPercent:F1}%, Memory: {metrics.MemoryPercent:F1}%"
                );
            }
            _logger.LogDebug("Scale-down cooldown active; skipping scale-down");
        }

        return ScalingDecision.None;
    }

    private bool ShouldScaleUp(ScalingMetrics metrics, int currentInstances)
    {
        return metrics.CpuPercent > _options.TargetCpuPercent
            || metrics.MemoryPercent > _options.TargetMemoryPercent
            || metrics.QueueLength > currentInstances * 10;
    }

    private bool ShouldScaleDown(ScalingMetrics metrics, int currentInstances)
    {
        return currentInstances > _options.MinInstances
            && metrics.CpuPercent < _options.TargetCpuPercent * 0.5
            && metrics.MemoryPercent < _options.TargetMemoryPercent * 0.5
            && metrics.QueueLength < currentInstances * 2;
    }

    private int CalculateScaleUpDelta(ScalingMetrics metrics, int currentInstances)
    {
        // Calculate required instances
        var cpuRatio = (double)metrics.CpuPercent / _options.TargetCpuPercent;
        var memoryRatio = (double)metrics.MemoryPercent / _options.TargetMemoryPercent;
        var targetRatio = Math.Max(cpuRatio, memoryRatio);

        var targetInstances = (int)Math.Ceiling(currentInstances * targetRatio);
        return Math.Max(1, targetInstances - currentInstances);
    }

    private async Task ApplyScalingAsync(
        int newCount,
        ScalingDecision decision,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Scaling from {Current} to {New} instances. Reason: {Reason}",
            CurrentInstances,
            newCount,
            decision.Reason
        );

        try
        {
            await _scaleAction(newCount, cancellationToken).ConfigureAwait(false);

            lock (_stateLock)
            {
                if (decision.Action == ScalingAction.ScaleUp)
                {
                    _lastScaleUp = _timeProvider.GetUtcNow();
                }
                else
                {
                    _lastScaleDown = _timeProvider.GetUtcNow();
                }
                _currentInstances = newCount;
            }

            _logger.LogInformation("Scaling complete, current instance count: {Count}", newCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scale to {NewCount} instances", newCount);
        }
    }

    /// <summary>
    /// Sets the current instance count (for testing).
    /// </summary>
    internal void SetCurrentInstances(int count)
    {
        lock (_stateLock)
        {
            _currentInstances = count;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _evaluateLock.Dispose();
    }
}
