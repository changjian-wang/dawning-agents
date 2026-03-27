namespace Dawning.Agents.Core.Evaluation;

using System.Text.Json;
using Dawning.Agents.Abstractions.Evaluation;
using Microsoft.Extensions.Logging;

/// <summary>
/// A/B test runner.
/// </summary>
/// <remarks>
/// Used to compare the effectiveness of two agents or configurations.
/// </remarks>
public sealed class ABTestRunner
{
    private readonly ILogger<ABTestRunner> _logger;

    public ABTestRunner(ILogger<ABTestRunner>? logger = null)
    {
        _logger =
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ABTestRunner>.Instance;
    }

    /// <summary>
    /// Runs an A/B test.
    /// </summary>
    public async Task<ABTestResult> RunAsync(
        IAgentEvaluator evaluatorA,
        IAgentEvaluator evaluatorB,
        IEnumerable<EvaluationTestCase> testCases,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(evaluatorA);
        ArgumentNullException.ThrowIfNull(evaluatorB);
        ArgumentNullException.ThrowIfNull(testCases);
        _logger.LogInformation("Starting A/B test: {A} vs {B}", evaluatorA.Name, evaluatorB.Name);

        var testCaseList = testCases.ToList();

        // Evaluate A and B in parallel
        var taskA = evaluatorA.EvaluateBatchAsync(testCaseList, cancellationToken);
        var taskB = evaluatorB.EvaluateBatchAsync(testCaseList, cancellationToken);

        await Task.WhenAll(taskA, taskB).ConfigureAwait(false);

        var reportA = await taskA.ConfigureAwait(false);
        var reportB = await taskB.ConfigureAwait(false);

        var result = new ABTestResult
        {
            VariantA = new ABVariant { Name = evaluatorA.Name, Report = reportA },
            VariantB = new ABVariant { Name = evaluatorB.Name, Report = reportB },
            TestCaseCount = testCaseList.Count,
        };

        _logger.LogInformation(
            "A/B test completed: {A} ({ScoreA:F1}) vs {B} ({ScoreB:F1}), Winner: {Winner}",
            evaluatorA.Name,
            reportA.AverageScore,
            evaluatorB.Name,
            reportB.AverageScore,
            result.Winner?.Name ?? "Tie"
        );

        return result;
    }
}

/// <summary>
/// A/B test result.
/// </summary>
public record ABTestResult
{
    /// <summary>
    /// Gets variant A.
    /// </summary>
    public required ABVariant VariantA { get; init; }

    /// <summary>
    /// Gets variant B.
    /// </summary>
    public required ABVariant VariantB { get; init; }

    /// <summary>
    /// Gets the test case count.
    /// </summary>
    public int TestCaseCount { get; init; }

    /// <summary>
    /// Gets the winner (variant with higher score).
    /// </summary>
    public ABVariant? Winner
    {
        get
        {
            if (Math.Abs(VariantA.Report.AverageScore - VariantB.Report.AverageScore) < 1.0)
            {
                return null; // Tie (difference less than 1 point)
            }

            return VariantA.Report.AverageScore > VariantB.Report.AverageScore
                ? VariantA
                : VariantB;
        }
    }

    /// <summary>
    /// Gets the score difference.
    /// </summary>
    public double ScoreDifference => VariantA.Report.AverageScore - VariantB.Report.AverageScore;

    /// <summary>
    /// Gets the pass rate difference.
    /// </summary>
    public double PassRateDifference => VariantA.Report.PassRate - VariantB.Report.PassRate;

    /// <summary>
    /// Gets the latency difference in milliseconds.
    /// </summary>
    public double LatencyDifference =>
        VariantA.Report.AverageLatencyMs - VariantB.Report.AverageLatencyMs;

    /// <summary>
    /// Gets the comparison timestamp.
    /// </summary>
    public DateTimeOffset ComparedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A/B test variant.
/// </summary>
public record ABVariant
{
    /// <summary>
    /// Gets the variant name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the evaluation report.
    /// </summary>
    public required EvaluationReport Report { get; init; }
}

/// <summary>
/// Evaluation report generator.
/// </summary>
public sealed class EvaluationReportGenerator
{
    private readonly ILogger<EvaluationReportGenerator> _logger;

    public EvaluationReportGenerator(ILogger<EvaluationReportGenerator>? logger = null)
    {
        _logger =
            logger
            ?? Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<EvaluationReportGenerator>
                .Instance;
    }

    /// <summary>
    /// Generates a JSON-formatted report.
    /// </summary>
    public string GenerateJson(EvaluationReport report)
    {
        return JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
    }

    /// <summary>
    /// Generates a Markdown-formatted report.
    /// </summary>
    public string GenerateMarkdown(EvaluationReport report)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"# Evaluation Report: {report.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Agent:** {report.AgentName}");
        sb.AppendLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Duration:** {report.DurationMs}ms");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Tests | {report.TotalCount} |");
        sb.AppendLine($"| Passed | {report.PassedCount} |");
        sb.AppendLine($"| Failed | {report.FailedCount} |");
        sb.AppendLine($"| Pass Rate | {report.PassRate:P1} |");
        sb.AppendLine($"| Average Score | {report.AverageScore:F1} |");
        sb.AppendLine($"| Avg Latency | {report.AverageLatencyMs:F0}ms |");
        sb.AppendLine($"| P50 Latency | {report.P50LatencyMs:F0}ms |");
        sb.AppendLine($"| P95 Latency | {report.P95LatencyMs:F0}ms |");
        sb.AppendLine($"| P99 Latency | {report.P99LatencyMs:F0}ms |");
        sb.AppendLine($"| Total Tokens | {report.TotalTokens:N0} |");
        sb.AppendLine($"| Est. Cost | ${report.TotalEstimatedCost:F4} |");
        sb.AppendLine();

        // Detailed results
        sb.AppendLine("## Detailed Results");
        sb.AppendLine();
        sb.AppendLine("| Test Case | Status | Score | Latency | Tools Called |");
        sb.AppendLine("|-----------|--------|-------|---------|--------------|");

        foreach (var result in report.Results)
        {
            var status = result.Passed ? "✅ Pass" : "❌ Fail";
            var tools = result.ToolsCalled != null ? string.Join(", ", result.ToolsCalled) : "-";
            sb.AppendLine(
                $"| {result.TestCaseId} | {status} | {result.Score:F1} | {result.LatencyMs}ms | {tools} |"
            );
        }

        sb.AppendLine();

        // Failure details
        var failedResults = report.Results.Where(r => !r.Passed).ToList();
        if (failedResults.Count > 0)
        {
            sb.AppendLine("## Failed Tests Details");
            sb.AppendLine();

            foreach (var result in failedResults)
            {
                sb.AppendLine($"### {result.TestCaseId}");
                sb.AppendLine();
                sb.AppendLine(
                    $"**Failure Reason:** {result.FailureReason ?? "Score below threshold"}"
                );
                sb.AppendLine();
                if (!string.IsNullOrEmpty(result.ActualOutput))
                {
                    sb.AppendLine("**Actual Output:**");
                    sb.AppendLine("```");
                    sb.AppendLine(result.ActualOutput);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Saves the report to files.
    /// </summary>
    public async Task SaveToFileAsync(
        EvaluationReport report,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(directory);

        var baseName = $"evaluation_{report.Id}";

        // Save JSON
        var jsonPath = Path.Combine(directory, $"{baseName}.json");
        await File.WriteAllTextAsync(jsonPath, GenerateJson(report), cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Saved JSON report to {Path}", jsonPath);

        // Save Markdown
        var mdPath = Path.Combine(directory, $"{baseName}.md");
        await File.WriteAllTextAsync(mdPath, GenerateMarkdown(report), cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Saved Markdown report to {Path}", mdPath);
    }
}
