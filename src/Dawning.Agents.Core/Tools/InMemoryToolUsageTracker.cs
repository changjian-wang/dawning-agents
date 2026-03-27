using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// In-memory tool usage tracker implementation.
/// </summary>
public sealed class InMemoryToolUsageTracker : IToolUsageTracker
{
    private readonly ConcurrentDictionary<string, ToolUsageAccumulator> _stats = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly ILogger<InMemoryToolUsageTracker> _logger;
    private readonly int _maxRecentErrors;

    /// <summary>
    /// Creates an in-memory tool usage tracker.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="maxRecentErrors">The maximum number of recent errors to retain.</param>
    public InMemoryToolUsageTracker(
        ILogger<InMemoryToolUsageTracker>? logger = null,
        int maxRecentErrors = 10
    )
    {
        _logger = logger ?? NullLogger<InMemoryToolUsageTracker>.Instance;
        _maxRecentErrors = maxRecentErrors;
    }

    /// <inheritdoc />
    public Task RecordUsageAsync(
        ToolUsageRecord record,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(record);

        var accumulator = _stats.GetOrAdd(
            record.ToolName,
            _ => new ToolUsageAccumulator(_maxRecentErrors)
        );
        accumulator.Add(record);

        _logger.LogDebug(
            "Recorded usage for tool {ToolName}: success={Success}, duration={Duration}ms",
            record.ToolName,
            record.Success,
            record.Duration.TotalMilliseconds
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ToolUsageStats> GetStatsAsync(
        string toolName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (_stats.TryGetValue(toolName, out var accumulator))
        {
            return Task.FromResult(accumulator.ToStats(toolName));
        }

        return Task.FromResult(
            new ToolUsageStats { ToolName = toolName, LastUsed = DateTimeOffset.MinValue }
        );
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolUsageStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = _stats.Select(kvp => kvp.Value.ToStats(kvp.Key)).ToList();
        return Task.FromResult<IReadOnlyList<ToolUsageStats>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolUsageStats>> GetLowUtilityToolsAsync(
        float successRateThreshold = 0.3f,
        int minCalls = 3,
        CancellationToken cancellationToken = default
    )
    {
        var result = _stats
            .Select(kvp => kvp.Value.ToStats(kvp.Key))
            .Where(s => s.TotalCalls >= minCalls && s.SuccessRate < successRateThreshold)
            .OrderBy(s => s.SuccessRate)
            .ToList();

        return Task.FromResult<IReadOnlyList<ToolUsageStats>>(result);
    }

    /// <summary>
    /// Thread-safe statistics accumulator.
    /// </summary>
    private sealed class ToolUsageAccumulator
    {
        private readonly object _lock = new();
        private readonly int _maxRecentErrors;
        private readonly Queue<string> _recentErrors;
        private int _totalCalls;
        private int _successCount;
        private long _totalDurationTicks;
        private DateTimeOffset _lastUsed;

        public ToolUsageAccumulator(int maxRecentErrors)
        {
            _maxRecentErrors = maxRecentErrors;
            _recentErrors = new Queue<string>(maxRecentErrors);
        }

        public void Add(ToolUsageRecord record)
        {
            lock (_lock)
            {
                _totalCalls++;
                _totalDurationTicks += record.Duration.Ticks;
                _lastUsed = record.Timestamp;

                if (record.Success)
                {
                    _successCount++;
                }
                else if (record.ErrorMessage is not null)
                {
                    if (_recentErrors.Count >= _maxRecentErrors)
                    {
                        _recentErrors.Dequeue();
                    }

                    _recentErrors.Enqueue(record.ErrorMessage);
                }
            }
        }

        public ToolUsageStats ToStats(string toolName)
        {
            lock (_lock)
            {
                var avgTicks = _totalCalls > 0 ? _totalDurationTicks / _totalCalls : 0;
                return new ToolUsageStats
                {
                    ToolName = toolName,
                    TotalCalls = _totalCalls,
                    SuccessCount = _successCount,
                    FailureCount = _totalCalls - _successCount,
                    AverageLatency = new TimeSpan(avgTicks),
                    LastUsed = _lastUsed,
                    RecentErrors = [.. _recentErrors],
                };
            }
        }
    }
}
