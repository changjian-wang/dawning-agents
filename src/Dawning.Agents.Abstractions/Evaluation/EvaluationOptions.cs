using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Evaluation;

/// <summary>
/// 评估指标类型
/// </summary>
public enum EvaluationMetric
{
    /// <summary>
    /// 精确匹配
    /// </summary>
    ExactMatch,

    /// <summary>
    /// 包含关键词
    /// </summary>
    ContainsKeywords,

    /// <summary>
    /// 语义相似度
    /// </summary>
    SemanticSimilarity,

    /// <summary>
    /// 工具调用准确性
    /// </summary>
    ToolCallAccuracy,

    /// <summary>
    /// 延迟
    /// </summary>
    Latency,

    /// <summary>
    /// Token 效率
    /// </summary>
    TokenEfficiency,

    /// <summary>
    /// LLM 评判
    /// </summary>
    LLMAsJudge,

    /// <summary>
    /// 自定义
    /// </summary>
    Custom,
}

/// <summary>
/// 评估配置选项
/// </summary>
public sealed class EvaluationOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Evaluation";

    /// <summary>
    /// 启用的指标
    /// </summary>
    public List<EvaluationMetric> EnabledMetrics { get; set; } =
    [
        EvaluationMetric.ContainsKeywords,
        EvaluationMetric.ToolCallAccuracy,
        EvaluationMetric.Latency,
    ];

    /// <summary>
    /// 通过阈值 (0-100)
    /// </summary>
    public double PassThreshold { get; set; } = 70;

    /// <summary>
    /// 最大并发评估数
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// 单个测试超时时间（秒）
    /// </summary>
    public int TestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 是否在失败时继续
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// 是否保存详细日志
    /// </summary>
    public bool SaveDetailedLogs { get; set; } = true;

    /// <summary>
    /// 报告输出目录
    /// </summary>
    public string? ReportOutputDirectory { get; set; }

    /// <summary>
    /// LLM-as-Judge 配置
    /// </summary>
    public LLMJudgeOptions? LLMJudge { get; set; }

    /// <summary>
    /// 语义相似度配置
    /// </summary>
    public SemanticSimilarityOptions? SemanticSimilarity { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (PassThreshold is < 0 or > 100)
        {
            throw new InvalidOperationException("PassThreshold must be between 0 and 100");
        }

        if (MaxConcurrency <= 0)
        {
            throw new InvalidOperationException("MaxConcurrency must be greater than 0");
        }

        if (TestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("TestTimeoutSeconds must be greater than 0");
        }

        LLMJudge?.Validate();
        SemanticSimilarity?.Validate();
    }
}

/// <summary>
/// LLM-as-Judge 配置
/// </summary>
public sealed class LLMJudgeOptions : IValidatableOptions
{
    /// <summary>
    /// 使用的模型
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// 评分温度
    /// </summary>
    public float Temperature { get; set; } = 0.0f;

    /// <summary>
    /// 评分提示词模板
    /// </summary>
    public string? PromptTemplate { get; set; }

    /// <summary>
    /// 评分维度
    /// </summary>
    public List<string> ScoringDimensions { get; set; } =
    ["Accuracy", "Relevance", "Completeness", "Clarity"];

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("LLMJudge Model is required");
        }

        if (Temperature is < 0f or > 2f)
        {
            throw new InvalidOperationException("LLMJudge Temperature must be between 0.0 and 2.0");
        }
    }
}

/// <summary>
/// 语义相似度配置
/// </summary>
public sealed class SemanticSimilarityOptions : IValidatableOptions
{
    /// <summary>
    /// 相似度阈值
    /// </summary>
    public float Threshold { get; set; } = 0.8f;

    /// <summary>
    /// 使用的 Embedding 模型
    /// </summary>
    public string? EmbeddingModel { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (Threshold is < 0f or > 1f)
        {
            throw new InvalidOperationException(
                "SemanticSimilarity Threshold must be between 0.0 and 1.0"
            );
        }
    }
}

/// <summary>
/// 评估指标权重配置
/// </summary>
public sealed class MetricWeights
{
    /// <summary>
    /// 精确匹配权重
    /// </summary>
    public double ExactMatch { get; set; } = 1.0;

    /// <summary>
    /// 关键词匹配权重
    /// </summary>
    public double ContainsKeywords { get; set; } = 0.8;

    /// <summary>
    /// 语义相似度权重
    /// </summary>
    public double SemanticSimilarity { get; set; } = 0.9;

    /// <summary>
    /// 工具调用准确性权重
    /// </summary>
    public double ToolCallAccuracy { get; set; } = 0.7;

    /// <summary>
    /// 延迟权重
    /// </summary>
    public double Latency { get; set; } = 0.3;

    /// <summary>
    /// Token 效率权重
    /// </summary>
    public double TokenEfficiency { get; set; } = 0.2;

    /// <summary>
    /// LLM 评判权重
    /// </summary>
    public double LLMAsJudge { get; set; } = 1.0;
}
