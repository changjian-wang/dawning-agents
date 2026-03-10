namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent 自动扩展器实现
/// </summary>
public sealed class AgentAutoScaler : IAgentAutoScaler, IDisposable
{
    private readonly ScalingOptions _options;
    private readonly Func<Task<ScalingMetrics>> _metricsProvider;
    private readonly Func<int, Task> _scaleAction;
    private readonly ILogger<AgentAutoScaler> _logger;

    private int _currentInstances;
    private DateTime? _lastScaleUp;
    private DateTime? _lastScaleDown;
    private readonly SemaphoreSlim _evaluateLock = new(1, 1);
    private readonly object _stateLock = new();
    private bool _disposed;

    public AgentAutoScaler(
        ScalingOptions options,
        Func<Task<ScalingMetrics>> metricsProvider,
        Func<int, Task> scaleAction,
        ILogger<AgentAutoScaler>? logger = null
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metricsProvider =
            metricsProvider ?? throw new ArgumentNullException(nameof(metricsProvider));
        _scaleAction = scaleAction ?? throw new ArgumentNullException(nameof(scaleAction));
        _logger = logger ?? NullLogger<AgentAutoScaler>.Instance;
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
    public DateTime? LastScaleUpTime
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
    public DateTime? LastScaleDownTime
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
        var metrics = await _metricsProvider().ConfigureAwait(false);

        await _evaluateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
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
                        "扩展评估: 无需操作 (CPU: {Cpu}%, Mem: {Mem}%, Queue: {Queue})",
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
            _evaluateLock.Release();
        }
    }

    private ScalingDecision MakeScalingDecision(ScalingMetrics metrics, int currentInstances)
    {
        var now = DateTime.UtcNow;

        // 检查是否需要扩容
        if (ShouldScaleUp(metrics, currentInstances))
        {
            var cooldown = TimeSpan.FromSeconds(_options.ScaleUpCooldownSeconds);
            if (!_lastScaleUp.HasValue || now - _lastScaleUp.Value > cooldown)
            {
                var delta = CalculateScaleUpDelta(metrics, currentInstances);
                return ScalingDecision.ScaleUp(
                    delta,
                    $"CPU: {metrics.CpuPercent:F1}%, 内存: {metrics.MemoryPercent:F1}%, 队列: {metrics.QueueLength}"
                );
            }
            _logger.LogDebug("扩容冷却中，跳过扩容");
        }

        // 检查是否可以缩容
        if (ShouldScaleDown(metrics, currentInstances))
        {
            var cooldown = TimeSpan.FromSeconds(_options.ScaleDownCooldownSeconds);
            if (!_lastScaleDown.HasValue || now - _lastScaleDown.Value > cooldown)
            {
                return ScalingDecision.ScaleDown(
                    1,
                    $"低利用率 - CPU: {metrics.CpuPercent:F1}%, 内存: {metrics.MemoryPercent:F1}%"
                );
            }
            _logger.LogDebug("缩容冷却中，跳过缩容");
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
        // 计算需要多少实例
        var cpuRatio = metrics.CpuPercent / _options.TargetCpuPercent;
        var memoryRatio = metrics.MemoryPercent / _options.TargetMemoryPercent;
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
            "从 {Current} 扩展到 {New} 个实例。原因：{Reason}",
            CurrentInstances,
            newCount,
            decision.Reason
        );

        try
        {
            await _scaleAction(newCount).ConfigureAwait(false);

            lock (_stateLock)
            {
                if (decision.Action == ScalingAction.ScaleUp)
                {
                    _lastScaleUp = DateTime.UtcNow;
                }
                else
                {
                    _lastScaleDown = DateTime.UtcNow;
                }
                _currentInstances = newCount;
            }

            _logger.LogInformation("扩展完成，当前实例数: {Count}", newCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扩展到 {NewCount} 个实例失败", newCount);
        }
    }

    /// <summary>
    /// 设置当前实例数（用于测试）
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
