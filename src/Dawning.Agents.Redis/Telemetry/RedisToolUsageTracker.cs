using System.Globalization;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Telemetry;

/// <summary>
/// A distributed tool usage tracker backed by Redis Hash.
/// </summary>
/// <remarks>
/// <para>Per-tool statistics are stored as Redis Hashes, supporting atomic increment operations.</para>
/// <para>Recent error lists use Redis List (LPUSH + LTRIM to maintain a fixed length).</para>
/// <para>Supports cross-process, multi-instance aggregate statistics.</para>
/// </remarks>
public sealed class RedisToolUsageTracker : IToolUsageTracker
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisToolUsageTracker> _logger;
    private readonly string _prefix;
    private readonly int _maxRecentErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisToolUsageTracker"/> class.
    /// </summary>
    public RedisToolUsageTracker(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options,
        ILogger<RedisToolUsageTracker>? logger = null,
        int maxRecentErrors = 10
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger ?? NullLogger<RedisToolUsageTracker>.Instance;
        _database = _connection.GetDatabase(_options.DefaultDatabase);
        _prefix = $"{_options.InstanceName}tool_usage:";
        _maxRecentErrors = maxRecentErrors;
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(
        ToolUsageRecord record,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(record);

        var hashKey = $"{_prefix}{record.ToolName}";
        var batch = _database.CreateBatch();

        // Atomically update statistics
        _ = batch.HashIncrementAsync(hashKey, "totalCalls", 1);
        _ = batch.HashIncrementAsync(hashKey, record.Success ? "successCount" : "failureCount", 1);
        _ = batch.HashIncrementAsync(
            hashKey,
            "totalDurationMs",
            (long)record.Duration.TotalMilliseconds
        );
        _ = batch.HashSetAsync(
            hashKey,
            "lastUsed",
            record.Timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
        );

        // Record error (if any)
        if (!record.Success && !string.IsNullOrEmpty(record.ErrorMessage))
        {
            var errorsKey = $"{hashKey}:errors";
            _ = batch.ListLeftPushAsync(errorsKey, record.ErrorMessage);
            _ = batch.ListTrimAsync(errorsKey, 0, _maxRecentErrors - 1);
        }

        batch.Execute();

        // Wait for critical operations to complete
        await _database.HashIncrementAsync(hashKey, "totalCalls", 0).ConfigureAwait(false); // force sync

        _logger.LogDebug(
            "Recorded usage for tool {ToolName}: success={Success}, duration={Duration}ms",
            record.ToolName,
            record.Success,
            record.Duration.TotalMilliseconds
        );
    }

    /// <inheritdoc />
    public async Task<ToolUsageStats> GetStatsAsync(
        string toolName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var hashKey = $"{_prefix}{toolName}";
        var entries = await _database.HashGetAllAsync(hashKey).ConfigureAwait(false);

        if (entries.Length == 0)
        {
            return new ToolUsageStats { ToolName = toolName, LastUsed = DateTimeOffset.MinValue };
        }

        var dict = entries.ToDictionary(
            e => e.Name.ToString(),
            e => e.Value.ToString(),
            StringComparer.OrdinalIgnoreCase
        );

        var totalCalls = GetIntValue(dict, "totalCalls");
        var successCount = GetIntValue(dict, "successCount");
        var failureCount = GetIntValue(dict, "failureCount");
        var totalDurationMs = GetLongValue(dict, "totalDurationMs");
        var lastUsedMs = GetLongValue(dict, "lastUsed");

        var avgLatency =
            totalCalls > 0
                ? TimeSpan.FromMilliseconds((double)totalDurationMs / totalCalls)
                : TimeSpan.Zero;

        var lastUsed =
            lastUsedMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(lastUsedMs)
                : DateTimeOffset.MinValue;

        // Retrieve recent errors
        var errorsKey = $"{hashKey}:errors";
        var recentErrors = await _database
            .ListRangeAsync(errorsKey, 0, _maxRecentErrors - 1)
            .ConfigureAwait(false);

        return new ToolUsageStats
        {
            ToolName = toolName,
            TotalCalls = totalCalls,
            SuccessCount = successCount,
            FailureCount = failureCount,
            AverageLatency = avgLatency,
            LastUsed = lastUsed,
            RecentErrors = recentErrors.Select(e => e.ToString()).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolUsageStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var server = GetServer();
        var pattern = $"{_prefix}*";
        var keys = server.Keys(database: _options.DefaultDatabase, pattern: pattern, pageSize: 100);

        var results = new List<ToolUsageStats>();
        foreach (var key in keys)
        {
            var keyStr = key.ToString();
            // Skip the errors list key
            if (keyStr.EndsWith(":errors", StringComparison.Ordinal))
            {
                continue;
            }

            var toolName = keyStr[_prefix.Length..];
            var stats = await GetStatsAsync(toolName, cancellationToken).ConfigureAwait(false);
            results.Add(stats);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolUsageStats>> GetLowUtilityToolsAsync(
        float successRateThreshold = 0.3f,
        int minCalls = 3,
        CancellationToken cancellationToken = default
    )
    {
        var allStats = await GetAllStatsAsync(cancellationToken).ConfigureAwait(false);

        return allStats
            .Where(s => s.TotalCalls >= minCalls && s.SuccessRate < successRateThreshold)
            .OrderBy(s => s.SuccessRate)
            .ToList();
    }

    private IServer GetServer()
    {
        var endpoints = _connection.GetEndPoints();
        return _connection.GetServer(endpoints[0]);
    }

    private static int GetIntValue(Dictionary<string, string> dict, string key) =>
        dict.TryGetValue(key, out var value)
        && int.TryParse(value, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;

    private static long GetLongValue(Dictionary<string, string> dict, string key) =>
        dict.TryGetValue(key, out var value)
        && long.TryParse(value, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
}
