using System.Globalization;
using System.Text.Json;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Telemetry;

/// <summary>
/// A distributed token usage tracker backed by Redis.
/// </summary>
/// <remarks>
/// <para>Global counters use INCRBY for atomic accumulation.</para>
/// <para>Individual records are stored as JSON in a Redis List (reverse chronological order).</para>
/// <para>Per-source/model/session statistics use Redis Hash for efficient aggregation.</para>
/// <para>Supports cross-process, multi-instance distributed token tracking.</para>
/// </remarks>
public sealed class RedisTokenUsageTracker : ITokenUsageTracker
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisTokenUsageTracker> _logger;
    private readonly string _prefix;
    private readonly int _maxRecords;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisTokenUsageTracker"/> class.
    /// </summary>
    /// <param name="connection">The Redis connection multiplexer.</param>
    /// <param name="options">The Redis configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="maxRecords">The maximum number of records to retain.</param>
    public RedisTokenUsageTracker(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options,
        ILogger<RedisTokenUsageTracker>? logger = null,
        int maxRecords = 10000
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger ?? NullLogger<RedisTokenUsageTracker>.Instance;
        _database = _connection.GetDatabase(_options.DefaultDatabase);
        _prefix = $"{_options.InstanceName}token_usage:";
        _maxRecords = maxRecords;
    }

    /// <inheritdoc />
    public long TotalPromptTokens
    {
        get
        {
            var val = _database.StringGet($"{_prefix}total:prompt");
            return val.HasValue ? (long)val : 0;
        }
    }

    /// <inheritdoc />
    public long TotalCompletionTokens
    {
        get
        {
            var val = _database.StringGet($"{_prefix}total:completion");
            return val.HasValue ? (long)val : 0;
        }
    }

    /// <inheritdoc />
    public long TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    /// <summary>
    /// Gets the total number of calls.
    /// </summary>
    public int CallCount
    {
        get
        {
            var val = _database.StringGet($"{_prefix}total:calls");
            return val.HasValue ? (int)val : 0;
        }
    }

    /// <inheritdoc />
    public void Record(TokenUsageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var batch = _database.CreateBatch();

        // Global counters
        _ = batch.StringIncrementAsync($"{_prefix}total:prompt", record.PromptTokens);
        _ = batch.StringIncrementAsync($"{_prefix}total:completion", record.CompletionTokens);
        _ = batch.StringIncrementAsync($"{_prefix}total:calls", 1);

        // Per-source statistics
        var sourceKey = $"{_prefix}source:{record.Source}";
        _ = batch.HashIncrementAsync(sourceKey, "prompt", record.PromptTokens);
        _ = batch.HashIncrementAsync(sourceKey, "completion", record.CompletionTokens);
        _ = batch.HashIncrementAsync(sourceKey, "calls", 1);

        // Per-model statistics
        if (!string.IsNullOrEmpty(record.Model))
        {
            _ = batch.HashIncrementAsync(
                $"{_prefix}model_totals",
                record.Model,
                record.TotalTokens
            );
        }

        // Per-session statistics
        if (!string.IsNullOrEmpty(record.SessionId))
        {
            _ = batch.HashIncrementAsync(
                $"{_prefix}session_totals",
                record.SessionId,
                record.TotalTokens
            );
        }

        // Store detailed record (retain most recent N entries)
        var json = JsonSerializer.Serialize(record, s_jsonOptions);
        var recordsKey = $"{_prefix}records";
        _ = batch.ListLeftPushAsync(recordsKey, json);
        _ = batch.ListTrimAsync(recordsKey, 0, _maxRecords - 1);

        batch.Execute();
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
        // Filtered queries require iterating through records
        if (!string.IsNullOrEmpty(source) || !string.IsNullOrEmpty(sessionId))
        {
            return GetFilteredSummary(source, sessionId);
        }

        // No filter: read directly from aggregate keys
        var promptVal = _database.StringGet($"{_prefix}total:prompt");
        var completionVal = _database.StringGet($"{_prefix}total:completion");
        var callsVal = _database.StringGet($"{_prefix}total:calls");

        var totalPrompt = promptVal.HasValue ? (long)promptVal : 0;
        var totalCompletion = completionVal.HasValue ? (long)completionVal : 0;
        var callCount = callsVal.HasValue ? (int)callsVal : 0;

        // By source
        var bySource = GetSourceUsages();

        // By model
        var byModel = GetHashTotals($"{_prefix}model_totals");

        // By session
        var bySession = GetHashTotals($"{_prefix}session_totals");

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
        var recordsKey = $"{_prefix}records";
        var allRecords = _database.ListRange(recordsKey, 0, _maxRecords - 1);

        var records = new List<TokenUsageRecord>();
        foreach (var entry in allRecords)
        {
            if (!entry.HasValue)
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<TokenUsageRecord>(
                    entry.ToString(),
                    s_jsonOptions
                );
                if (record is null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(source) && record.Source != source)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(sessionId) && record.SessionId != sessionId)
                {
                    continue;
                }

                records.Add(record);
            }
            catch (JsonException)
            {
                // Skip unparseable records
            }
        }

        return records;
    }

    /// <inheritdoc />
    public void Reset(string? source = null, string? sessionId = null)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(sessionId))
        {
            // Full reset
            var server = GetServer();
            var keys = server.Keys(
                database: _options.DefaultDatabase,
                pattern: $"{_prefix}*",
                pageSize: 100
            );

            foreach (var key in keys)
            {
                _database.KeyDelete(key);
            }

            _logger.LogInformation("All token usage statistics have been reset");
        }
        else if (!string.IsNullOrEmpty(source))
        {
            _database.KeyDelete($"{_prefix}source:{source}");
            _logger.LogInformation(
                "Token usage statistics have been reset: source={Source}",
                source
            );
        }
    }

    private TokenUsageSummary GetFilteredSummary(string? source, string? sessionId)
    {
        var records = GetRecords(source, sessionId);

        if (records.Count == 0)
        {
            return TokenUsageSummary.Empty;
        }

        var totalPrompt = records.Sum(r => (long)r.PromptTokens);
        var totalCompletion = records.Sum(r => (long)r.CompletionTokens);

        var bySource = records
            .GroupBy(r => r.Source)
            .ToDictionary(
                g => g.Key,
                g => new SourceUsage(
                    g.Sum(r => (long)r.PromptTokens),
                    g.Sum(r => (long)r.CompletionTokens),
                    g.Count()
                )
            );

        var byModel = records
            .Where(r => r.Model != null)
            .GroupBy(r => r.Model!)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens));

        var bySession = records
            .Where(r => r.SessionId != null)
            .GroupBy(r => r.SessionId!)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalTokens));

        return new TokenUsageSummary(
            totalPrompt,
            totalCompletion,
            records.Count,
            bySource,
            byModel.Count > 0 ? byModel : null,
            bySession.Count > 0 ? bySession : null
        );
    }

    private Dictionary<string, SourceUsage> GetSourceUsages()
    {
        var server = GetServer();
        var keys = server.Keys(
            database: _options.DefaultDatabase,
            pattern: $"{_prefix}source:*",
            pageSize: 100
        );

        var result = new Dictionary<string, SourceUsage>();
        foreach (var key in keys)
        {
            var sourceName = key.ToString()[($"{_prefix}source:".Length)..];
            var entries = _database.HashGetAll(key);
            var dict = entries.ToDictionary(
                e => e.Name.ToString(),
                e => e.Value.ToString(),
                StringComparer.OrdinalIgnoreCase
            );

            var prompt = GetLongValue(dict, "prompt");
            var completion = GetLongValue(dict, "completion");
            var calls = (int)GetLongValue(dict, "calls");

            result[sourceName] = new SourceUsage(prompt, completion, calls);
        }

        return result;
    }

    private Dictionary<string, long> GetHashTotals(string key)
    {
        var entries = _database.HashGetAll(key);
        return entries.ToDictionary(
            e => e.Name.ToString(),
            e => e.Value.HasValue ? (long)e.Value : 0
        );
    }

    private IServer GetServer()
    {
        var endpoints = _connection.GetEndPoints();
        return _connection.GetServer(endpoints[0]);
    }

    private static long GetLongValue(Dictionary<string, string> dict, string key) =>
        dict.TryGetValue(key, out var value)
        && long.TryParse(value, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
}
