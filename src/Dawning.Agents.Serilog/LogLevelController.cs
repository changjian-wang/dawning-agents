using Dawning.Agents.Abstractions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Dawning.Agents.Serilog;

/// <summary>
/// 日志级别控制器实现
/// </summary>
public sealed class LogLevelController : ILogLevelController
{
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly Lock _lock = new();
    private readonly Dictionary<long, LogEventLevel> _temporaryLevels = [];
    private long _scopeId;
    private LogEventLevel _baseLevel;

    public LogLevelController(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
        _baseLevel = _levelSwitch.MinimumLevel;
    }

    /// <inheritdoc />
    public string CurrentLevel => _levelSwitch.MinimumLevel.ToString();

    /// <inheritdoc />
    public void SetLevel(string level)
    {
        var logLevel = ParseLevel(level);

        lock (_lock)
        {
            _baseLevel = logLevel;
            ApplyEffectiveLevelUnsafe();
        }
    }

    /// <inheritdoc />
    public IDisposable TemporaryLevel(string level, TimeSpan duration)
    {
        lock (_lock)
        {
            var targetLevel = ParseLevel(level);
            var id = Interlocked.Increment(ref _scopeId);

            _temporaryLevels[id] = targetLevel;
            ApplyEffectiveLevelUnsafe();

            return new TemporaryLevelScope(this, id, duration);
        }
    }

    private void RemoveTemporaryLevel(long id)
    {
        lock (_lock)
        {
            _temporaryLevels.Remove(id);
            ApplyEffectiveLevelUnsafe();
        }
    }

    private void ApplyEffectiveLevelUnsafe()
    {
        if (_temporaryLevels.Count == 0)
        {
            _levelSwitch.MinimumLevel = _baseLevel;
            return;
        }

        var latestId = _temporaryLevels.Keys.Max();
        _levelSwitch.MinimumLevel = _temporaryLevels[latestId];
    }

    private static LogEventLevel ParseLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private sealed class TemporaryLevelScope : IDisposable
    {
        private readonly LogLevelController _controller;
        private readonly long _scopeId;
        private readonly CancellationTokenSource _cts;
        private readonly Task _restoreTask;
        private int _disposed;

        public TemporaryLevelScope(LogLevelController controller, long scopeId, TimeSpan duration)
        {
            _controller = controller;
            _scopeId = scopeId;
            _cts = new CancellationTokenSource();

            // 自动恢复任务
            _restoreTask = Task.Delay(duration, _cts.Token)
                .ContinueWith(
                    _ =>
                    {
                        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                        {
                            _controller.RemoveTemporaryLevel(_scopeId);
                            try
                            {
                                _cts.Dispose();
                            }
                            catch (ObjectDisposedException) { }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default
                );
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException) { }

            try
            {
                _cts.Dispose();
            }
            catch (ObjectDisposedException) { }

            _controller.RemoveTemporaryLevel(_scopeId);
        }
    }
}
