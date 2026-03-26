using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Evaluation;

/// <summary>
/// Specifies the evaluation metric type.
/// </summary>
public enum EvaluationMetric
{
    /// <summary>
    /// Exact match comparison.
    /// </summary>
    ExactMatch,

    /// <summary>
    /// Contains specified keywords.
    /// </summary>
    ContainsKeywords,

    /// <summary>
    /// Semantic similarity comparison.
    /// </summary>
    SemanticSimilarity,

    /// <summary>
    /// Tool call accuracy.
    /// </summary>
    ToolCallAccuracy,

    /// <summary>
    /// Response latency.
    /// </summary>
    Latency,

    /// <summary>
    /// Token efficiency.
    /// </summary>
    TokenEfficiency,

    /// <summary>
    /// LLM as judge.
    /// </summary>
    LLMAsJudge,

    /// <summary>
    /// Custom metric.
    /// </summary>
    Custom,
}

/// <summary>
/// Configuration options for evaluation.
/// </summary>
public sealed class EvaluationOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Evaluation";

    /// <summary>
    /// The list of enabled evaluation metrics.
    /// </summary>
    public List<EvaluationMetric> EnabledMetrics { get; set; } =
    [
        EvaluationMetric.ContainsKeywords,
        EvaluationMetric.ToolCallAccuracy,
        EvaluationMetric.Latency,
    ];

    /// <summary>
    /// The pass threshold score (0-100).
    /// </summary>
    public double PassThreshold { get; set; } = 70;

    /// <summary>
    /// The maximum number of concurrent evaluations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// The timeout for a single test in seconds.
    /// </summary>
    public int TestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets a value indicating whether to continue on failure.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to save detailed logs.
    /// </summary>
    public bool SaveDetailedLogs { get; set; } = true;

    /// <summary>
    /// The report output directory.
    /// </summary>
    public string? ReportOutputDirectory { get; set; }

    /// <summary>
    /// The LLM-as-Judge configuration options.
    /// </summary>
    public LLMJudgeOptions? LLMJudge { get; set; }

    /// <summary>
    /// The semantic similarity configuration options.
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
/// Configuration options for LLM-as-Judge evaluation.
/// </summary>
public sealed class LLMJudgeOptions : IValidatableOptions
{
    /// <summary>
    /// The model to use for evaluation.
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// The temperature for scoring.
    /// </summary>
    public float Temperature { get; set; } = 0.0f;

    /// <summary>
    /// The prompt template for scoring.
    /// </summary>
    public string? PromptTemplate { get; set; }

    /// <summary>
    /// The scoring dimensions.
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

        if (ScoringDimensions == null || ScoringDimensions.Count == 0)
        {
            throw new InvalidOperationException(
                "LLMJudge ScoringDimensions must contain at least one dimension"
            );
        }

        if (ScoringDimensions.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "LLMJudge ScoringDimensions must not contain empty items"
            );
        }
    }
}

/// <summary>
/// Configuration options for semantic similarity evaluation.
/// </summary>
public sealed class SemanticSimilarityOptions : IValidatableOptions
{
    /// <summary>
    /// The similarity threshold.
    /// </summary>
    public float Threshold { get; set; } = 0.8f;

    /// <summary>
    /// The embedding model to use.
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
/// Configuration for evaluation metric weights.
/// </summary>
public sealed class MetricWeights
{
    /// <summary>
    /// The weight for exact match.
    /// </summary>
    public double ExactMatch { get; set; } = 1.0;

    /// <summary>
    /// The weight for keyword matching.
    /// </summary>
    public double ContainsKeywords { get; set; } = 0.8;

    /// <summary>
    /// The weight for semantic similarity.
    /// </summary>
    public double SemanticSimilarity { get; set; } = 0.9;

    /// <summary>
    /// The weight for tool call accuracy.
    /// </summary>
    public double ToolCallAccuracy { get; set; } = 0.7;

    /// <summary>
    /// The weight for latency.
    /// </summary>
    public double Latency { get; set; } = 0.3;

    /// <summary>
    /// The weight for token efficiency.
    /// </summary>
    public double TokenEfficiency { get; set; } = 0.2;

    /// <summary>
    /// The weight for LLM as judge.
    /// </summary>
    public double LLMAsJudge { get; set; } = 1.0;
}
