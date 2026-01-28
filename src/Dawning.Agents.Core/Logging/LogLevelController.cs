using Dawning.Agents.Abstractions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Dawning.Agents.Core.Logging;

/// <summary>
/// 日志级别控制器实现
/// </summary>
public class LogLevelController : ILogLevelController
{
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly Lock _lock = new();

    public LogLevelController(LoggingLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
    }

    /// <inheritdoc />
    public string CurrentLevel => _levelSwitch.MinimumLevel.ToString();

    /// <inheritdoc />
    public void SetLevel(string level)
    {
        var logLevel = ParseLevel(level);
        _levelSwitch.MinimumLevel = logLevel;
    }

    /// <inheritdoc />
    public IDisposable TemporaryLevel(string level, TimeSpan duration)
    {
        lock (_lock)
        {
            var originalLevel = _levelSwitch.MinimumLevel;
            var targetLevel = ParseLevel(level);

            _levelSwitch.MinimumLevel = targetLevel;

            return new TemporaryLevelScope(this, originalLevel, duration);
        }
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
        private readonly LogEventLevel _originalLevel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _restoreTask;
        private bool _disposed;

        public TemporaryLevelScope(
            LogLevelController controller,
            LogEventLevel originalLevel,
            TimeSpan duration
        )
        {
            _controller = controller;
            _originalLevel = originalLevel;
            _cts = new CancellationTokenSource();

            // 自动恢复任务
            _restoreTask = Task.Delay(duration, _cts.Token).ContinueWith(
                _ =>
                {
                    if (!_disposed)
                    {
                        Restore();
                    }
                },
                TaskContinuationOptions.OnlyOnRanToCompletion
            );
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            Restore();
        }

        private void Restore()
        {
            lock (_controller._lock)
            {
                _controller._levelSwitch.MinimumLevel = _originalLevel;
            }
        }
    }
}
