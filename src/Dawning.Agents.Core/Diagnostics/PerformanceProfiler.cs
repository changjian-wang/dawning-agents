using System.Collections.Concurrent;
using System.Diagnostics;
using Dawning.Agents.Abstractions.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Diagnostics;

/// <summary>
/// Performance profiler implementation.
/// </summary>
public sealed class PerformanceProfiler : IPerformanceProfiler
{
    private readonly ConcurrentQueue<OperationTrace> _traces = new();
    private readonly ConcurrentDictionary<string, OperationStatistics> _statistics = new();
    private readonly ILogger<PerformanceProfiler> _logger;
    private readonly int _maxTraceCount;
    private readonly TimeSpan _slowOperationThreshold;

    public PerformanceProfiler(
        ILogger<PerformanceProfiler>? logger = null,
        int maxTraceCount = 10000,
        TimeSpan? slowOperationThreshold = null
    )
    {
        _logger = logger ?? NullLogger<PerformanceProfiler>.Instance;
        _maxTraceCount = maxTraceCount;
        _slowOperationThreshold = slowOperationThreshold ?? TimeSpan.FromSeconds(1);
    }

    /// <inheritdoc />
    public IDisposable StartOperation(string operationName, string? category = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return new OperationScope(this, operationName, category);
    }

    /// <inheritdoc />
    public void RecordOperation(
        string operationName,
        TimeSpan duration,
        string? category = null,
        IReadOnlyDictionary<string, object>? metadata = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        RecordOperationInternal(
            operationName,
            duration,
            category,
            isSuccess: true,
            errorMessage: null,
            metadata
        );
    }

    internal void RecordOperationInternal(
        string operationName,
        TimeSpan duration,
        string? category,
        bool isSuccess,
        string? errorMessage,
        IReadOnlyDictionary<string, object>? metadata
    )
    {
        var trace = new OperationTrace
        {
            OperationName = operationName,
            Category = category,
            StartTime = DateTimeOffset.UtcNow - duration,
            Duration = duration,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            Metadata = metadata,
        };

        // Add to trace queue
        _traces.Enqueue(trace);

        // Limit queue size
        while (_traces.Count > _maxTraceCount && _traces.TryDequeue(out _)) { }

        // Update statistics
        UpdateStatistics(operationName, duration, isSuccess);

        // Slow operation warning
        if (duration > _slowOperationThreshold)
        {
            _logger.LogWarning(
                "Slow operation detected: {OperationName} ({Category}) took {Duration:F2}ms",
                operationName,
                category ?? "Unknown",
                duration.TotalMilliseconds
            );
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<OperationTrace> GetSlowOperations(TimeSpan threshold, int limit = 100)
    {
        return _traces
            .Where(t => t.Duration > threshold)
            .OrderByDescending(t => t.Duration)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, OperationStatistics> GetStatistics(string? category = null)
    {
        if (string.IsNullOrEmpty(category))
        {
            return _statistics.ToDictionary(kvp => kvp.Key, kvp => SnapshotStatistics(kvp.Value));
        }

        return _statistics
            .Where(kvp => kvp.Key.StartsWith($"{category}:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => SnapshotStatistics(kvp.Value));
    }

    private static OperationStatistics SnapshotStatistics(OperationStatistics source)
    {
        lock (source)
        {
            return new OperationStatistics
            {
                OperationName = source.OperationName,
                TotalCount = source.TotalCount,
                SuccessCount = source.SuccessCount,
                FailureCount = source.FailureCount,
                TotalDuration = source.TotalDuration,
                MinDuration = source.MinDuration,
                MaxDuration = source.MaxDuration,
                LastCallTime = source.LastCallTime,
            };
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        while (_traces.TryDequeue(out _)) { }

        _statistics.Clear();
    }

    private void UpdateStatistics(string operationName, TimeSpan duration, bool isSuccess)
    {
        _statistics.AddOrUpdate(
            operationName,
            _ => new OperationStatistics
            {
                OperationName = operationName,
                TotalCount = 1,
                SuccessCount = isSuccess ? 1 : 0,
                FailureCount = isSuccess ? 0 : 1,
                TotalDuration = duration,
                MinDuration = duration,
                MaxDuration = duration,
                LastCallTime = DateTimeOffset.UtcNow,
            },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.TotalCount++;
                    if (isSuccess)
                    {
                        existing.SuccessCount++;
                    }
                    else
                    {
                        existing.FailureCount++;
                    }

                    existing.TotalDuration += duration;
                    if (duration < existing.MinDuration)
                    {
                        existing.MinDuration = duration;
                    }

                    if (duration > existing.MaxDuration)
                    {
                        existing.MaxDuration = duration;
                    }

                    existing.LastCallTime = DateTimeOffset.UtcNow;
                }

                return existing;
            }
        );
    }

    /// <summary>
    /// Operation timing scope.
    /// </summary>
    private sealed class OperationScope : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly string? _category;
        private readonly Stopwatch _stopwatch;
        private volatile bool _disposed;
        private string? _errorMessage;

        public OperationScope(PerformanceProfiler profiler, string operationName, string? category)
        {
            _profiler = profiler;
            _operationName = operationName;
            _category = category;
            _stopwatch = Stopwatch.StartNew();
        }

        public void SetError(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();

            _profiler.RecordOperationInternal(
                _operationName,
                _stopwatch.Elapsed,
                _category,
                isSuccess: string.IsNullOrEmpty(_errorMessage),
                _errorMessage,
                metadata: null
            );
        }
    }
}

/// <summary>
/// Performance profiler extension methods.
/// </summary>
public static class PerformanceProfilerExtensions
{
    /// <summary>
    /// Profiles an LLM call.
    /// </summary>
    public static IDisposable ProfileLLMCall(
        this IPerformanceProfiler profiler,
        string modelName,
        string? provider = null
    )
    {
        var operationName = $"{OperationCategories.LLM}:{provider ?? "Unknown"}:{modelName}";
        return profiler.StartOperation(operationName, OperationCategories.LLM);
    }

    /// <summary>
    /// Profiles a tool execution.
    /// </summary>
    public static IDisposable ProfileToolExecution(
        this IPerformanceProfiler profiler,
        string toolName
    )
    {
        var operationName = $"{OperationCategories.Tool}:{toolName}";
        return profiler.StartOperation(operationName, OperationCategories.Tool);
    }

    /// <summary>
    /// Profiles an agent execution.
    /// </summary>
    public static IDisposable ProfileAgentExecution(
        this IPerformanceProfiler profiler,
        string agentName
    )
    {
        var operationName = $"{OperationCategories.Agent}:{agentName}";
        return profiler.StartOperation(operationName, OperationCategories.Agent);
    }

    /// <summary>
    /// Profiles a RAG retrieval.
    /// </summary>
    public static IDisposable ProfileRAGRetrieval(
        this IPerformanceProfiler profiler,
        string knowledgeBaseName
    )
    {
        var operationName = $"{OperationCategories.RAG}:{knowledgeBaseName}";
        return profiler.StartOperation(operationName, OperationCategories.RAG);
    }

    /// <summary>
    /// Gets LLM call statistics.
    /// </summary>
    public static IReadOnlyDictionary<string, OperationStatistics> GetLLMStatistics(
        this IPerformanceProfiler profiler
    )
    {
        return profiler.GetStatistics(OperationCategories.LLM);
    }

    /// <summary>
    /// Gets tool execution statistics.
    /// </summary>
    public static IReadOnlyDictionary<string, OperationStatistics> GetToolStatistics(
        this IPerformanceProfiler profiler
    )
    {
        return profiler.GetStatistics(OperationCategories.Tool);
    }
}
