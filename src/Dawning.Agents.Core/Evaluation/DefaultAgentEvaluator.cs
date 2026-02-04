namespace Dawning.Agents.Core.Evaluation;

using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// 默认 Agent 评估器实现
/// </summary>
public sealed class DefaultAgentEvaluator : IAgentEvaluator
{
    private readonly IAgent _agent;
    private readonly EvaluationOptions _options;
    private readonly ILogger<DefaultAgentEvaluator> _logger;
    private readonly List<IMetricEvaluator> _metricEvaluators;

    public DefaultAgentEvaluator(
        IAgent agent,
        IOptions<EvaluationOptions> options,
        IEnumerable<IMetricEvaluator>? metricEvaluators = null,
        ILogger<DefaultAgentEvaluator>? logger = null
    )
    {
        _agent = agent;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultAgentEvaluator>.Instance;
        _metricEvaluators = metricEvaluators?.ToList() ?? [];

        // 添加默认指标评估器
        if (_metricEvaluators.Count == 0)
        {
            _metricEvaluators.Add(new KeywordMatchEvaluator());
            _metricEvaluators.Add(new ToolCallAccuracyEvaluator());
            _metricEvaluators.Add(new LatencyEvaluator());
        }
    }

    public string Name => $"Evaluator[{_agent.Name}]";

    public async Task<EvaluationResult> EvaluateAsync(
        EvaluationTestCase testCase,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Evaluating test case: {TestCaseId}", testCase.Id);

        var stopwatch = Stopwatch.StartNew();
        var metricScores = new Dictionary<string, double>();
        string? actualOutput = null;
        List<string>? toolsCalled = null;
        int stepCount = 0;
        string? failureReason = null;
        TokenUsage? tokenUsage = null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TestTimeoutSeconds));

            // 执行 Agent
            var response = await _agent.RunAsync(testCase.Input, cts.Token);

            stopwatch.Stop();

            actualOutput = response.FinalAnswer;
            toolsCalled = response.Steps
                .Where(s => s.Action != null)
                .Select(s => s.Action!)
                .ToList();
            stepCount = response.Steps.Count;

            // 估算 Token 使用（简单估算）
            tokenUsage = EstimateTokenUsage(testCase.Input, actualOutput ?? "");

            // 计算各项指标
            var context = new MetricEvaluationContext
            {
                TestCase = testCase,
                ActualOutput = actualOutput,
                ToolsCalled = toolsCalled,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                StepCount = stepCount,
                TokenUsage = tokenUsage,
            };

            foreach (var evaluator in _metricEvaluators)
            {
                var score = await evaluator.EvaluateAsync(context, cancellationToken);
                metricScores[evaluator.MetricName] = score;
                _logger.LogTrace("Metric {Metric}: {Score}", evaluator.MetricName, score);
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            failureReason = "Evaluation timed out";
            _logger.LogWarning("Test case {TestCaseId} timed out", testCase.Id);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            failureReason = ex.Message;
            _logger.LogError(ex, "Error evaluating test case {TestCaseId}", testCase.Id);
        }

        // 计算总分
        var totalScore = metricScores.Count > 0
            ? metricScores.Values.Average() * 100
            : 0;

        var passed = failureReason == null && totalScore >= _options.PassThreshold;

        return new EvaluationResult
        {
            TestCaseId = testCase.Id,
            Passed = passed,
            Score = totalScore,
            ActualOutput = actualOutput,
            LatencyMs = stopwatch.ElapsedMilliseconds,
            TokenUsage = tokenUsage,
            ToolsCalled = toolsCalled,
            StepCount = stepCount,
            MetricScores = metricScores,
            FailureReason = failureReason,
        };
    }

    public async Task<EvaluationReport> EvaluateBatchAsync(
        IEnumerable<EvaluationTestCase> testCases,
        CancellationToken cancellationToken = default
    )
    {
        var testCaseList = testCases.ToList();
        _logger.LogInformation("Starting batch evaluation of {Count} test cases", testCaseList.Count);

        var overallStopwatch = Stopwatch.StartNew();
        var results = new List<EvaluationResult>();

        // 使用信号量限制并发
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);
        var tasks = testCaseList.Select(async testCase =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await EvaluateAsync(testCase, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        if (_options.ContinueOnFailure)
        {
            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks);
        }
        else
        {
            foreach (var task in tasks)
            {
                var result = await task;
                results.Add(result);

                if (!result.Passed)
                {
                    _logger.LogWarning("Stopping evaluation due to failure: {TestCaseId}", result.TestCaseId);
                    break;
                }
            }
        }

        overallStopwatch.Stop();

        var report = new EvaluationReport
        {
            Name = $"Evaluation_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            AgentName = _agent.Name,
            Results = results,
            DurationMs = overallStopwatch.ElapsedMilliseconds,
        };

        _logger.LogInformation(
            "Batch evaluation completed: {Passed}/{Total} passed ({PassRate:P1}), Avg Score: {AvgScore:F1}",
            report.PassedCount,
            report.TotalCount,
            report.PassRate,
            report.AverageScore
        );

        return report;
    }

    private static TokenUsage EstimateTokenUsage(string input, string output)
    {
        // 简单估算：英文约 4 字符/token，中文约 1.5 字符/token
        var inputTokens = EstimateTokens(input);
        var outputTokens = EstimateTokens(output);

        return new TokenUsage
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            // 假设 GPT-4 价格: $0.03/1K input, $0.06/1K output
            EstimatedCost = inputTokens * 0.00003m + outputTokens * 0.00006m,
        };
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var englishCount = text.Length - chineseCount;

        return (int)(chineseCount / 1.5 + englishCount / 4);
    }
}

/// <summary>
/// 指标评估上下文
/// </summary>
public record MetricEvaluationContext
{
    public required EvaluationTestCase TestCase { get; init; }
    public string? ActualOutput { get; init; }
    public List<string>? ToolsCalled { get; init; }
    public long LatencyMs { get; init; }
    public int StepCount { get; init; }
    public TokenUsage? TokenUsage { get; init; }
}

/// <summary>
/// 指标评估器接口
/// </summary>
public interface IMetricEvaluator
{
    string MetricName { get; }
    Task<double> EvaluateAsync(MetricEvaluationContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 关键词匹配评估器
/// </summary>
public sealed class KeywordMatchEvaluator : IMetricEvaluator
{
    public string MetricName => "KeywordMatch";

    public Task<double> EvaluateAsync(MetricEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var keywords = context.TestCase.ExpectedKeywords;
        if (keywords == null || keywords.Count == 0 || string.IsNullOrEmpty(context.ActualOutput))
        {
            return Task.FromResult(1.0); // 无关键词要求时满分
        }

        var matchedCount = keywords.Count(k =>
            context.ActualOutput.Contains(k, StringComparison.OrdinalIgnoreCase)
        );

        return Task.FromResult((double)matchedCount / keywords.Count);
    }
}

/// <summary>
/// 工具调用准确性评估器
/// </summary>
public sealed class ToolCallAccuracyEvaluator : IMetricEvaluator
{
    public string MetricName => "ToolCallAccuracy";

    public Task<double> EvaluateAsync(MetricEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var expectedTools = context.TestCase.ExpectedTools;
        if (expectedTools == null || expectedTools.Count == 0)
        {
            return Task.FromResult(1.0); // 无工具要求时满分
        }

        var actualTools = context.ToolsCalled ?? [];
        if (actualTools.Count == 0)
        {
            return Task.FromResult(0.0); // 期望有工具调用但没有
        }

        // 计算交集
        var intersection = expectedTools.Intersect(actualTools, StringComparer.OrdinalIgnoreCase).Count();
        var union = expectedTools.Union(actualTools, StringComparer.OrdinalIgnoreCase).Count();

        return Task.FromResult(union > 0 ? (double)intersection / union : 0.0);
    }
}

/// <summary>
/// 延迟评估器
/// </summary>
public sealed class LatencyEvaluator : IMetricEvaluator
{
    public string MetricName => "Latency";

    public Task<double> EvaluateAsync(MetricEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var maxLatency = context.TestCase.MaxLatencyMs;
        if (maxLatency == null || maxLatency <= 0)
        {
            return Task.FromResult(1.0); // 无延迟要求时满分
        }

        if (context.LatencyMs <= maxLatency)
        {
            return Task.FromResult(1.0); // 在限制内满分
        }

        // 超出限制时，按比例扣分（最低 0 分）
        var ratio = (double)maxLatency.Value / context.LatencyMs;
        return Task.FromResult(Math.Max(0, ratio));
    }
}

/// <summary>
/// 精确匹配评估器
/// </summary>
public sealed class ExactMatchEvaluator : IMetricEvaluator
{
    public string MetricName => "ExactMatch";

    public Task<double> EvaluateAsync(MetricEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var expected = context.TestCase.ExpectedOutput;
        if (string.IsNullOrEmpty(expected))
        {
            return Task.FromResult(1.0); // 无精确匹配要求时满分
        }

        var actual = context.ActualOutput ?? string.Empty;
        return Task.FromResult(
            string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0
        );
    }
}
