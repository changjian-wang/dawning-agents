using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Telemetry;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// 基于内存的 Token 使用追踪器实现
/// </summary>
/// <remarks>
/// 线程安全的内存实现，适用于单进程场景。
/// 对于分布式场景，可以实现基于 Redis 或数据库的追踪器。
/// </remarks>
public sealed class InMemoryTokenUsageTracker : ITokenUsageTracker
{
    private readonly ConcurrentBag<TokenUsageRecord> _records = [];
    private readonly Lock _resetLock = new();
    private long _totalPromptTokens;
    private long _totalCompletionTokens;
    private int _callCount;

    /// <inheritdoc />
    public long TotalPromptTokens => Volatile.Read(ref _totalPromptTokens);

    /// <inheritdoc />
    public long TotalCompletionTokens => Volatile.Read(ref _totalCompletionTokens);

    /// <inheritdoc />
    public long TotalTokens =>
        Volatile.Read(ref _totalPromptTokens) + Volatile.Read(ref _totalCompletionTokens);

    /// <inheritdoc />
    public int CallCount => _callCount;

    /// <inheritdoc />
    public void Record(TokenUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_resetLock)
        {
            _records.Add(record);
            Interlocked.Add(ref _totalPromptTokens, (long)record.PromptTokens);
            Interlocked.Add(ref _totalCompletionTokens, (long)record.CompletionTokens);
            Interlocked.Increment(ref _callCount);
        }
    }

    /// <inheritdoc />
    public void Record(
        string source,
        int promptTokens,
        int completionTokens,
        string? model = null,
        string? sessionId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Record(TokenUsageRecord.Create(source, promptTokens, completionTokens, model, sessionId));
    }

    /// <inheritdoc />
    public TokenUsageSummary GetSummary(string? source = null, string? sessionId = null)
    {
        var filteredRecords = FilterRecords(source, sessionId);

        if (filteredRecords.Count == 0)
        {
            return TokenUsageSummary.Empty;
        }

        var totalPrompt = filteredRecords.Sum(r => (long)r.PromptTokens);
        var totalCompletion = filteredRecords.Sum(r => (long)r.CompletionTokens);
        var callCount = filteredRecords.Count;

        // 按来源分组
        var bySource = filteredRecords
            .GroupBy(r => r.Source)
            .ToDictionary(
                g => g.Key,
                g => new SourceUsage(
                    g.Sum(r => (long)r.PromptTokens),
                    g.Sum(r => (long)r.CompletionTokens),
                    g.Count()
                )
            );

        // 按模型分组
        var byModel = filteredRecords
            .Where(r => r.Model != null)
            .GroupBy(r => r.Model!)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens));

        // 按会话分组
        var bySession = filteredRecords
            .Where(r => r.SessionId != null)
            .GroupBy(r => r.SessionId!)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens));

        return new TokenUsageSummary(
            totalPrompt,
            totalCompletion,
            callCount,
            bySource,
            byModel.Count > 0 ? byModel : null,
            bySession.Count > 0 ? bySession : null
        );
    }

    /// <inheritdoc />
    public IReadOnlyList<TokenUsageRecord> GetRecords(
        string? source = null,
        string? sessionId = null
    )
    {
        return FilterRecords(source, sessionId);
    }

    /// <inheritdoc />
    public void Reset(string? source = null, string? sessionId = null)
    {
        lock (_resetLock)
        {
            if (source == null && sessionId == null)
            {
                // 全部重置
                _records.Clear();
                Interlocked.Exchange(ref _totalPromptTokens, 0L);
                Interlocked.Exchange(ref _totalCompletionTokens, 0L);
                Interlocked.Exchange(ref _callCount, 0);
            }
            else
            {
                // 部分重置：由于 ConcurrentBag 不支持删除，需要重建
                var remaining = _records
                    .Where(r =>
                        (source == null || r.Source != source)
                        && (sessionId == null || r.SessionId != sessionId)
                    )
                    .ToList();

                _records.Clear();
                foreach (var record in remaining)
                {
                    _records.Add(record);
                }

                // 重新计算总数
                Interlocked.Exchange(
                    ref _totalPromptTokens,
                    remaining.Sum(r => (long)r.PromptTokens)
                );
                Interlocked.Exchange(
                    ref _totalCompletionTokens,
                    remaining.Sum(r => (long)r.CompletionTokens)
                );
                Interlocked.Exchange(ref _callCount, remaining.Count);
            }
        }
    }

    private List<TokenUsageRecord> FilterRecords(string? source, string? sessionId)
    {
        IEnumerable<TokenUsageRecord> records = _records;

        if (source != null)
        {
            records = records.Where(r => r.Source == source);
        }

        if (sessionId != null)
        {
            records = records.Where(r => r.SessionId == sessionId);
        }

        return records.ToList();
    }
}
