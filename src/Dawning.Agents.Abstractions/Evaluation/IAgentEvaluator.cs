namespace Dawning.Agents.Abstractions.Evaluation;

/// <summary>
/// Agent 评估器接口
/// </summary>
public interface IAgentEvaluator
{
    /// <summary>
    /// 评估器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 评估单个测试用例
    /// </summary>
    Task<EvaluationResult> EvaluateAsync(
        EvaluationTestCase testCase,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 批量评估测试用例
    /// </summary>
    Task<EvaluationReport> EvaluateBatchAsync(
        IEnumerable<EvaluationTestCase> testCases,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 评估测试用例
/// </summary>
public record EvaluationTestCase
{
    /// <summary>
    /// 测试用例 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 测试用例名称
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 输入内容
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// 期望输出（用于精确匹配）
    /// </summary>
    public string? ExpectedOutput { get; init; }

    /// <summary>
    /// 期望包含的关键词
    /// </summary>
    public IReadOnlyList<string>? ExpectedKeywords { get; init; }

    /// <summary>
    /// 期望调用的工具
    /// </summary>
    public IReadOnlyList<string>? ExpectedTools { get; init; }

    /// <summary>
    /// 评估标准描述（用于 LLM-as-Judge）
    /// </summary>
    public string? EvaluationCriteria { get; init; }

    /// <summary>
    /// 最大允许延迟（毫秒）
    /// </summary>
    public int? MaxLatencyMs { get; init; }

    /// <summary>
    /// 最大允许 Token 数
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// 评估结果
/// </summary>
public record EvaluationResult
{
    /// <summary>
    /// 测试用例 ID
    /// </summary>
    public required string TestCaseId { get; init; }

    /// <summary>
    /// 是否通过
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// 总分 (0-100)
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// 实际输出
    /// </summary>
    public string? ActualOutput { get; init; }

    /// <summary>
    /// 延迟（毫秒）
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// Token 使用量
    /// </summary>
    public TokenUsage? TokenUsage { get; init; }

    /// <summary>
    /// 调用的工具
    /// </summary>
    public IReadOnlyList<string>? ToolsCalled { get; init; }

    /// <summary>
    /// 执行步骤数
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// 各项指标得分
    /// </summary>
    public IReadOnlyDictionary<string, double>? MetricScores { get; init; }

    /// <summary>
    /// 失败原因
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 评估时间
    /// </summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 元数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Token 使用量
/// </summary>
public record TokenUsage
{
    /// <summary>
    /// 输入 Token 数
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// 输出 Token 数
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// 估算成本（美元）
    /// </summary>
    public decimal? EstimatedCost { get; init; }
}

/// <summary>
/// 评估报告
/// </summary>
public record EvaluationReport
{
    /// <summary>
    /// 报告 ID
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 报告名称
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// 所有评估结果
    /// </summary>
    public required List<EvaluationResult> Results { get; init; }

    /// <summary>
    /// 总测试用例数
    /// </summary>
    public int TotalCount => Results.Count;

    /// <summary>
    /// 通过数
    /// </summary>
    public int PassedCount => Results.Count(r => r.Passed);

    /// <summary>
    /// 失败数
    /// </summary>
    public int FailedCount => Results.Count(r => !r.Passed);

    /// <summary>
    /// 通过率
    /// </summary>
    public double PassRate => TotalCount > 0 ? (double)PassedCount / TotalCount : 0;

    /// <summary>
    /// 平均得分
    /// </summary>
    public double AverageScore => Results.Count > 0 ? Results.Average(r => r.Score) : 0;

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AverageLatencyMs => Results.Count > 0 ? Results.Average(r => r.LatencyMs) : 0;

    /// <summary>
    /// P50 延迟（毫秒）
    /// </summary>
    public double P50LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 50);

    /// <summary>
    /// P95 延迟（毫秒）
    /// </summary>
    public double P95LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 95);

    /// <summary>
    /// P99 延迟（毫秒）
    /// </summary>
    public double P99LatencyMs => CalculatePercentile(Results.Select(r => (double)r.LatencyMs), 99);

    /// <summary>
    /// 总 Token 使用量
    /// </summary>
    public int TotalTokens =>
        Results.Where(r => r.TokenUsage != null).Sum(r => r.TokenUsage!.TotalTokens);

    /// <summary>
    /// 总估算成本
    /// </summary>
    public decimal TotalEstimatedCost =>
        Results
            .Where(r => r.TokenUsage?.EstimatedCost != null)
            .Sum(r => r.TokenUsage!.EstimatedCost!.Value);

    /// <summary>
    /// 生成时间
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    private static double CalculatePercentile(IEnumerable<double> values, int percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }
}
