using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Telemetry;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// In-memory token usage tracker implementation.
/// </summary>
/// <remarks>
/// Thread-safe in-memory implementation suitable for single-process scenarios.
/// For distributed scenarios, implement a Redis- or database-backed tracker.
/// </remarks>
public sealed class InMemoryTokenUsageTracker : ITokenUsageTracker
{
    private readonly ConcurrentBag<TokenUsageRecord> _records = [];
    private readonly Lock _resetLock = new();
    private long _totalPromptTokens;
    private long _totalCompletionTokens;
    private int _callCount;

    /// <inheritdoc />
    public long TotalPromptTokens
    {
        get
        {
            lock (_resetLock)
            {
                return _totalPromptTokens;
            }
        }
    }

    /// <inheritdoc />
    public long TotalCompletionTokens
    {
        get
        {
            lock (_resetLock)
            {
                return _totalCompletionTokens;
            }
        }
    }

    /// <inheritdoc />
    public long TotalTokens
    {
        get
        {
            lock (_resetLock)
            {
                return _totalPromptTokens + _totalCompletionTokens;
            }
        }
    }

    /// <inheritdoc />
    public int CallCount
    {
        get
        {
            lock (_resetLock)
            {
                return _callCount;
            }
        }
    }

    /// <inheritdoc />
    public void Record(TokenUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_resetLock)
        {
            _records.Add(record);
            _totalPromptTokens += record.PromptTokens;
            _totalCompletionTokens += record.CompletionTokens;
            _callCount++;
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

        // Group by source
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

        // Group by model
        var byModel = filteredRecords
            .Where(r => r.Model != null)
            .GroupBy(r => r.Model!)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens));

        // Group by session
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
                // Full reset
                _records.Clear();
                _totalPromptTokens = 0;
                _totalCompletionTokens = 0;
                _callCount = 0;
            }
            else
            {
                // Partial reset: ConcurrentBag does not support removal, so rebuild
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

                // Recalculate totals
                _totalPromptTokens = remaining.Sum(r => (long)r.PromptTokens);
                _totalCompletionTokens = remaining.Sum(r => (long)r.CompletionTokens);
                _callCount = remaining.Count;
            }
        }
    }

    private List<TokenUsageRecord> FilterRecords(string? source, string? sessionId)
    {
        lock (_resetLock)
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
}
