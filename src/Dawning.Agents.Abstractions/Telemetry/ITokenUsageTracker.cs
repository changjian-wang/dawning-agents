namespace Dawning.Agents.Abstractions.Telemetry;

/// <summary>
/// Defines a tracker for recording and aggregating token usage.
/// </summary>
/// <remarks>
/// Records and aggregates token consumption across LLM calls.
/// Supports grouping by source (agent), model, and session.
///
/// Usage example:
/// <code>
/// // Record token usage
/// tracker.Record(TokenUsageRecord.Create("MyAgent", 100, 50, "gpt-4"));
///
/// // Retrieve summary
/// var summary = tracker.GetSummary();
/// Console.WriteLine($"Total: {summary.TotalTokens} tokens");
/// </code>
/// </remarks>
public interface ITokenUsageTracker
{
    /// <summary>
    /// Records a token usage entry.
    /// </summary>
    /// <param name="record">The token usage record.</param>
    void Record(TokenUsageRecord record);

    /// <summary>
    /// Records a token usage entry using individual values.
    /// </summary>
    /// <param name="source">The source identifier.</param>
    /// <param name="promptTokens">The number of prompt tokens.</param>
    /// <param name="completionTokens">The number of completion tokens.</param>
    /// <param name="model">The model name.</param>
    /// <param name="sessionId">The session identifier.</param>
    void Record(
        string source,
        int promptTokens,
        int completionTokens,
        string? model = null,
        string? sessionId = null
    );

    /// <summary>
    /// Gets an aggregated usage summary.
    /// </summary>
    /// <param name="source">Optional source filter.</param>
    /// <param name="sessionId">Optional session filter.</param>
    /// <returns>The aggregated <see cref="TokenUsageSummary"/>.</returns>
    TokenUsageSummary GetSummary(string? source = null, string? sessionId = null);

    /// <summary>
    /// Gets all recorded token usage entries.
    /// </summary>
    /// <param name="source">Optional source filter.</param>
    /// <param name="sessionId">Optional session filter.</param>
    /// <returns>A read-only list of <see cref="TokenUsageRecord"/> entries.</returns>
    IReadOnlyList<TokenUsageRecord> GetRecords(string? source = null, string? sessionId = null);

    /// <summary>
    /// Resets tracked usage data.
    /// </summary>
    /// <param name="source">Optional source filter. When <see langword="null"/>, all data is reset.</param>
    /// <param name="sessionId">Optional session filter.</param>
    void Reset(string? source = null, string? sessionId = null);

    /// <summary>
    /// Gets the total number of prompt tokens.
    /// </summary>
    long TotalPromptTokens { get; }

    /// <summary>
    /// Gets the total number of completion tokens.
    /// </summary>
    long TotalCompletionTokens { get; }

    /// <summary>
    /// Gets the total number of tokens.
    /// </summary>
    long TotalTokens { get; }

    /// <summary>
    /// Gets the total number of LLM calls.
    /// </summary>
    int CallCount { get; }
}
