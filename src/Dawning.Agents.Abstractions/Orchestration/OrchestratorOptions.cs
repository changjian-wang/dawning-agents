using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Orchestration;

/// <summary>
/// Orchestrator configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
/// <code>
/// {
///   "Orchestration": {
///     "MaxConcurrency": 5,
///     "TimeoutSeconds": 300,
///     "ContinueOnError": false,
///     "ResultAggregationStrategy": "LastResult"
///   }
/// }
/// </code>
/// </remarks>
public class OrchestratorOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Orchestration";

    /// <summary>
    /// Maximum concurrency (for parallel orchestration).
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Total timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Per-Agent timeout in seconds.
    /// </summary>
    public int AgentTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to continue execution after an Agent failure.
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Result aggregation strategy (for parallel orchestration).
    /// </summary>
    public ResultAggregationStrategy AggregationStrategy { get; set; } =
        ResultAggregationStrategy.LastResult;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (MaxConcurrency < 1)
        {
            throw new InvalidOperationException("MaxConcurrency must be at least 1");
        }

        if (TimeoutSeconds < 1)
        {
            throw new InvalidOperationException("TimeoutSeconds must be at least 1");
        }

        if (AgentTimeoutSeconds < 1)
        {
            throw new InvalidOperationException("AgentTimeoutSeconds must be at least 1");
        }

        if (AgentTimeoutSeconds > TimeoutSeconds)
        {
            throw new InvalidOperationException(
                "AgentTimeoutSeconds must not exceed TimeoutSeconds"
            );
        }
    }
}

/// <summary>
/// Result aggregation strategy.
/// </summary>
public enum ResultAggregationStrategy
{
    /// <summary>
    /// Uses the last Agent's result.
    /// </summary>
    LastResult,

    /// <summary>
    /// Uses the first successful result.
    /// </summary>
    FirstSuccess,

    /// <summary>
    /// Merges all results.
    /// </summary>
    Merge,

    /// <summary>
    /// Votes to select the best result.
    /// </summary>
    Vote,

    /// <summary>
    /// Custom aggregation.
    /// </summary>
    Custom,
}
