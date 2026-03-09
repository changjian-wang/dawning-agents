namespace Dawning.Agents.Core.Evaluation;

using System.Text.Json;
using Dawning.Agents.Abstractions.Evaluation;
using Microsoft.Extensions.Logging;

/// <summary>
/// A/B 测试运行器
/// </summary>
/// <remarks>
/// 用于比较两个 Agent 或配置的效果差异。
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
    /// 运行 A/B 测试
    /// </summary>
    public async Task<ABTestResult> RunAsync(
        IAgentEvaluator evaluatorA,
        IAgentEvaluator evaluatorB,
        IEnumerable<EvaluationTestCase> testCases,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Starting A/B test: {A} vs {B}", evaluatorA.Name, evaluatorB.Name);

        var testCaseList = testCases.ToList();

        // 并行评估 A 和 B
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
/// A/B 测试结果
/// </summary>
public record ABTestResult
{
    /// <summary>
    /// 变体 A
    /// </summary>
    public required ABVariant VariantA { get; init; }

    /// <summary>
    /// 变体 B
    /// </summary>
    public required ABVariant VariantB { get; init; }

    /// <summary>
    /// 测试用例数
    /// </summary>
    public int TestCaseCount { get; init; }

    /// <summary>
    /// 获胜者（得分更高的变体）
    /// </summary>
    public ABVariant? Winner
    {
        get
        {
            if (Math.Abs(VariantA.Report.AverageScore - VariantB.Report.AverageScore) < 1.0)
            {
                return null; // 平局（差距小于 1 分）
            }

            return VariantA.Report.AverageScore > VariantB.Report.AverageScore
                ? VariantA
                : VariantB;
        }
    }

    /// <summary>
    /// 得分差异
    /// </summary>
    public double ScoreDifference => VariantA.Report.AverageScore - VariantB.Report.AverageScore;

    /// <summary>
    /// 通过率差异
    /// </summary>
    public double PassRateDifference => VariantA.Report.PassRate - VariantB.Report.PassRate;

    /// <summary>
    /// 延迟差异（毫秒）
    /// </summary>
    public double LatencyDifference =>
        VariantA.Report.AverageLatencyMs - VariantB.Report.AverageLatencyMs;

    /// <summary>
    /// 比较时间
    /// </summary>
    public DateTime ComparedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A/B 测试变体
/// </summary>
public record ABVariant
{
    /// <summary>
    /// 变体名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 评估报告
    /// </summary>
    public required EvaluationReport Report { get; init; }
}

/// <summary>
/// 评估报告生成器
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
    /// 生成 JSON 格式报告
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
    /// 生成 Markdown 格式报告
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

        // 摘要
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

        // 详细结果
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

        // 失败详情
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
    /// 保存报告到文件
    /// </summary>
    public async Task SaveToFileAsync(
        EvaluationReport report,
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(directory);

        var baseName = $"evaluation_{report.Id}";

        // 保存 JSON
        var jsonPath = Path.Combine(directory, $"{baseName}.json");
        await File.WriteAllTextAsync(jsonPath, GenerateJson(report), cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Saved JSON report to {Path}", jsonPath);

        // 保存 Markdown
        var mdPath = Path.Combine(directory, $"{baseName}.md");
        await File.WriteAllTextAsync(mdPath, GenerateMarkdown(report), cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Saved Markdown report to {Path}", mdPath);
    }
}
