namespace Dawning.Agents.Abstractions.Telemetry;

/// <summary>
/// Represents a single token usage record.
/// </summary>
/// <param name="Source">The source identifier (e.g., agent name or component name).</param>
/// <param name="PromptTokens">The number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">The number of completion tokens consumed.</param>
/// <param name="Timestamp">The time the record was created.</param>
/// <param name="Model">The model name used for the call.</param>
/// <param name="SessionId">An optional session identifier for grouped aggregation.</param>
/// <param name="Metadata">Optional metadata associated with this record.</param>
public record TokenUsageRecord(
    string Source,
    int PromptTokens,
    int CompletionTokens,
    DateTimeOffset Timestamp,
    string? Model = null,
    string? SessionId = null,
    IReadOnlyDictionary<string, object>? Metadata = null
)
{
    /// <summary>
    /// Gets the total number of tokens (prompt + completion).
    /// </summary>
    public long TotalTokens => (long)PromptTokens + CompletionTokens;

    /// <summary>
    /// Creates a new <see cref="TokenUsageRecord"/> with the current UTC timestamp.
    /// </summary>
    public static TokenUsageRecord Create(
        string source,
        int promptTokens,
        int completionTokens,
        string? model = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, object>? metadata = null
    )
    {
        return new TokenUsageRecord(
            source,
            promptTokens,
            completionTokens,
            DateTimeOffset.UtcNow,
            model,
            sessionId,
            metadata
        );
    }
}

/// <summary>
/// Represents an aggregated token usage summary.
/// </summary>
/// <param name="TotalPromptTokens">The total number of prompt tokens.</param>
/// <param name="TotalCompletionTokens">The total number of completion tokens.</param>
/// <param name="CallCount">The total number of LLM calls.</param>
/// <param name="BySource">Usage statistics grouped by source.</param>
/// <param name="ByModel">Usage statistics grouped by model.</param>
/// <param name="BySession">Usage statistics grouped by session.</param>
public record TokenUsageSummary(
    long TotalPromptTokens,
    long TotalCompletionTokens,
    int CallCount,
    IReadOnlyDictionary<string, SourceUsage> BySource,
    IReadOnlyDictionary<string, long>? ByModel = null,
    IReadOnlyDictionary<string, long>? BySession = null
)
{
    /// <summary>
    /// Gets the total number of tokens (prompt + completion).
    /// </summary>
    public long TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    /// <summary>
    /// Gets an empty summary with zero values.
    /// </summary>
    public static TokenUsageSummary Empty => new(0, 0, 0, new Dictionary<string, SourceUsage>());
}

/// <summary>
/// Represents token usage statistics for a single source.
/// </summary>
/// <param name="PromptTokens">The number of prompt tokens.</param>
/// <param name="CompletionTokens">The number of completion tokens.</param>
/// <param name="CallCount">The number of LLM calls.</param>
public record SourceUsage(long PromptTokens, long CompletionTokens, int CallCount)
{
    /// <summary>
    /// Gets the total number of tokens (prompt + completion).
    /// </summary>
    public long TotalTokens => (long)PromptTokens + CompletionTokens;
}
