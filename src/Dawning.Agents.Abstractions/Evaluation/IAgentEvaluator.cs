namespace Dawning.Agents.Abstractions.Evaluation;

/// <summary>
/// Defines the interface for agent evaluation.
/// </summary>
public interface IAgentEvaluator
{
    /// <summary>
    /// The name of the evaluator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates a single test case.
    /// </summary>
    Task<EvaluationResult> EvaluateAsync(
        EvaluationTestCase testCase,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates a batch of test cases.
    /// </summary>
    Task<EvaluationReport> EvaluateBatchAsync(
        IEnumerable<EvaluationTestCase> testCases,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Represents an evaluation test case.
/// </summary>
public record EvaluationTestCase
{
    /// <summary>
    /// The test case ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The test case name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The input content.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// The expected output for exact match comparison.
    /// </summary>
    public string? ExpectedOutput { get; init; }

    /// <summary>
    /// The expected keywords to match.
    /// </summary>
    public IReadOnlyList<string>? ExpectedKeywords { get; init; }

    /// <summary>
    /// The expected tools to be called.
    /// </summary>
    public IReadOnlyList<string>? ExpectedTools { get; init; }

    /// <summary>
    /// The evaluation criteria description for LLM-as-Judge.
    /// </summary>
    public string? EvaluationCriteria { get; init; }

    /// <summary>
    /// The maximum allowed latency in milliseconds.
    /// </summary>
    public int? MaxLatencyMs { get; init; }

    /// <summary>
    /// The maximum allowed token count.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// The tags.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Metadata
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents an evaluation result.
/// </summary>
public record EvaluationResult
{
    /// <summary>
    /// The test case ID.
    /// </summary>
    public required string TestCaseId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the test passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// The overall score (0-100).
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// The actual output.
    /// </summary>
    public string? ActualOutput { get; init; }

    /// <summary>
    /// The latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// The token usage.
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// The tools that were called.
    /// </summary>
    public IReadOnlyList<string>? ToolsCalled { get; init; }

    /// <summary>
    /// The number of execution steps.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// The individual metric scores.
    /// </summary>
    public IReadOnlyDictionary<string, double>? MetricScores { get; init; }

    /// <summary>
    /// The failure reason.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// The time when the evaluation was performed.
    /// </summary>
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Metadata
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents token usage statistics.
/// </summary>
public record TokenUsage
{
    /// <summary>
    /// The number of input tokens.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// The number of output tokens.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// The total number of tokens.
    /// </summary>
    public long TotalTokens => (long)InputTokens + OutputTokens;

    /// <summary>
    /// The estimated cost in US dollars.
    /// </summary>
    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// Represents an evaluation report.
/// </summary>
public record EvaluationReport
{
    /// <summary>
    /// The report ID.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The report name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The agent name.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// All evaluation results.
    /// </summary>
    public required IReadOnlyList<EvaluationResult> Results { get; init; }

    /// <summary>
    /// The total number of test cases.
    /// </summary>
    public int TotalCount => Results.Count;

    /// <summary>
    /// The number of passed test cases.
    /// </summary>
    public int PassedCount => Results.Count(r => r.Passed);

    /// <summary>
    /// The number of failed test cases.
    /// </summary>
    public int FailedCount => Results.Count(r => !r.Passed);

    /// <summary>
    /// The pass rate.
    /// </summary>
    public double PassRate => TotalCount > 0 ? (double)PassedCount / TotalCount : 0;

    /// <summary>
    /// The average score.
    /// </summary>
    public double AverageScore => Results.Count > 0 ? Results.Average(r => r.Score) : 0;

    /// <summary>
    /// The average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs => Results.Count > 0 ? Results.Average(r => r.LatencyMs) : 0;

    /// <summary>
    /// The P50 (median) latency in milliseconds.
    /// </summary>
    public double P50LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 50);

    /// <summary>
    /// The P95 latency in milliseconds.
    /// </summary>
    public double P95LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 95);

    /// <summary>
    /// The P99 latency in milliseconds.
    /// </summary>
    public double P99LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 99);

    /// <summary>
    /// The total token usage.
    /// </summary>
    public long TotalTokens =>
        Results.Where(r => r.TokenUsage != null).Sum(r => r.TokenUsage!.TotalTokens);

    /// <summary>
    /// The total estimated cost.
    /// </summary>
    public decimal TotalEstimatedCost =>
        Results
            .Where(r => r.TokenUsage?.EstimatedCost != null)
            .Sum(r => r.TokenUsage!.EstimatedCost!.Value);

    /// <summary>
    /// The time when the report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The duration in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Metadata
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    private static double CalculatePercentile(IEnumerable<double> values, int percentile)
    {
        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentile),
                "Percentile must be between 0 and 100."
            );
        }

        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }
}
