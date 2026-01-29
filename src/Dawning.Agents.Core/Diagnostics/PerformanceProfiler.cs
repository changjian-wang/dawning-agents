using System.Collections.Concurrent;
using System.Diagnostics;
using Dawning.Agents.Abstractions.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Diagnostics;

/// <summary>
/// 性能分析器实现
/// </summary>
public class PerformanceProfiler : IPerformanceProfiler
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
        return new OperationScope(this, operationName, category);
    }

    /// <inheritdoc />
    public void RecordOperation(
        string operationName,
        TimeSpan duration,
        string? category = null,
        IDictionary<string, object>? metadata = null
    )
    {
        RecordOperationInternal(operationName, duration, category, isSuccess: true, errorMessage: null, metadata);
    }

    internal void RecordOperationInternal(
        string operationName,
        TimeSpan duration,
        string? category,
        bool isSuccess,
        string? errorMessage,
        IDictionary<string, object>? metadata
    )
    {
        var trace = new OperationTrace
        {
            OperationName = operationName,
            Category = category,
            StartTime = DateTime.UtcNow - duration,
            Duration = duration,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            Metadata = metadata,
        };

        // 添加到追踪队列
        _traces.Enqueue(trace);

        // 限制队列大小
        while (_traces.Count > _maxTraceCount && _traces.TryDequeue(out _))
        {
        }

        // 更新统计信息
        UpdateStatistics(operationName, duration, isSuccess);

        // 慢操作警告
        if (duration > _slowOperationThreshold)
        {
            _logger.LogWarning(
                "慢操作检测: {OperationName} ({Category}) 耗时 {Duration:F2}ms",
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
            return _statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return _statistics
            .Where(kvp => kvp.Key.StartsWith($"{category}:", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <inheritdoc />
    public void Clear()
    {
        while (_traces.TryDequeue(out _))
        {
        }

        _statistics.Clear();
    }

    private void UpdateStatistics(string operationName, TimeSpan duration, bool isSuccess)
    {
        _statistics.AddOrUpdate(
            operationName,
            _ =>
                new OperationStatistics
                {
                    OperationName = operationName,
                    TotalCount = 1,
                    SuccessCount = isSuccess ? 1 : 0,
                    FailureCount = isSuccess ? 0 : 1,
                    TotalDuration = duration,
                    MinDuration = duration,
                    MaxDuration = duration,
                    LastCallTime = DateTime.UtcNow,
                },
            (_, existing) =>
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

                existing.LastCallTime = DateTime.UtcNow;
                return existing;
            }
        );
    }

    /// <summary>
    /// 操作计时作用域
    /// </summary>
    private sealed class OperationScope : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly string? _category;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
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
/// 性能分析器扩展方法
/// </summary>
public static class PerformanceProfilerExtensions
{
    /// <summary>
    /// 记录 LLM 调用
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
    /// 记录工具执行
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
    /// 记录 Agent 执行
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
    /// 记录 RAG 检索
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
    /// 获取 LLM 调用统计
    /// </summary>
    public static IReadOnlyDictionary<string, OperationStatistics> GetLLMStatistics(
        this IPerformanceProfiler profiler
    )
    {
        return profiler.GetStatistics(OperationCategories.LLM);
    }

    /// <summary>
    /// 获取工具执行统计
    /// </summary>
    public static IReadOnlyDictionary<string, OperationStatistics> GetToolStatistics(
        this IPerformanceProfiler profiler
    )
    {
        return profiler.GetStatistics(OperationCategories.Tool);
    }
}
