using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Model pricing information.
/// </summary>
/// <remarks>
/// Price unit: USD per 1K tokens.
/// Data source: official pricing from each LLM provider.
/// </remarks>
public class ModelPricing
{
    /// <summary>
    /// Model name.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Input price (USD per 1K tokens).
    /// </summary>
    public decimal InputPricePerKToken { get; init; }

    /// <summary>
    /// Output price (USD per 1K tokens).
    /// </summary>
    public decimal OutputPricePerKToken { get; init; }

    /// <summary>
    /// Calculates the total cost.
    /// </summary>
    public decimal CalculateCost(int inputTokens, int outputTokens)
    {
        return (inputTokens * InputPricePerKToken / 1000m)
            + (outputTokens * OutputPricePerKToken / 1000m);
    }

    /// <summary>
    /// Predefined model pricing (2026 data).
    /// </summary>
    public static class KnownPricing
    {
        // OpenAI models

        /// <summary>GPT-4o pricing.</summary>
        public static readonly ModelPricing Gpt4o = new()
        {
            Model = "gpt-4o",
            InputPricePerKToken = 0.0025m,
            OutputPricePerKToken = 0.01m,
        };

        /// <summary>GPT-4o Mini pricing.</summary>
        public static readonly ModelPricing Gpt4oMini = new()
        {
            Model = "gpt-4o-mini",
            InputPricePerKToken = 0.00015m,
            OutputPricePerKToken = 0.0006m,
        };

        /// <summary>GPT-4 Turbo pricing.</summary>
        public static readonly ModelPricing Gpt4Turbo = new()
        {
            Model = "gpt-4-turbo",
            InputPricePerKToken = 0.01m,
            OutputPricePerKToken = 0.03m,
        };

        /// <summary>GPT-3.5 Turbo pricing.</summary>
        public static readonly ModelPricing Gpt35Turbo = new()
        {
            Model = "gpt-3.5-turbo",
            InputPricePerKToken = 0.0005m,
            OutputPricePerKToken = 0.0015m,
        };

        // Claude models

        /// <summary>Claude 3 Opus pricing.</summary>
        public static readonly ModelPricing Claude3Opus = new()
        {
            Model = "claude-3-opus",
            InputPricePerKToken = 0.015m,
            OutputPricePerKToken = 0.075m,
        };

        /// <summary>Claude 3 Sonnet pricing.</summary>
        public static readonly ModelPricing Claude3Sonnet = new()
        {
            Model = "claude-3-sonnet",
            InputPricePerKToken = 0.003m,
            OutputPricePerKToken = 0.015m,
        };

        /// <summary>Claude 3 Haiku pricing.</summary>
        public static readonly ModelPricing Claude3Haiku = new()
        {
            Model = "claude-3-haiku",
            InputPricePerKToken = 0.00025m,
            OutputPricePerKToken = 0.00125m,
        };

        // Local models (free)

        /// <summary>Ollama local model pricing (free).</summary>
        public static readonly ModelPricing Ollama = new()
        {
            Model = "ollama",
            InputPricePerKToken = 0m,
            OutputPricePerKToken = 0m,
        };

        /// <summary>
        /// Gets pricing by model name.
        /// </summary>
        public static ModelPricing GetPricing(string model)
        {
            return model.ToLowerInvariant() switch
            {
                "gpt-4o" => Gpt4o,
                "gpt-4o-mini" => Gpt4oMini,
                "gpt-4-turbo" or "gpt-4-turbo-preview" => Gpt4Turbo,
                "gpt-3.5-turbo" or "gpt-35-turbo" => Gpt35Turbo,
                "claude-3-opus" or "claude-3-opus-20240229" => Claude3Opus,
                "claude-3-sonnet" or "claude-3-5-sonnet" => Claude3Sonnet,
                "claude-3-haiku" => Claude3Haiku,
                _ when model.Contains("ollama", StringComparison.OrdinalIgnoreCase) => Ollama,
                _ => new ModelPricing
                {
                    Model = model,
                    InputPricePerKToken = 0.001m, // default estimate
                    OutputPricePerKToken = 0.002m,
                },
            };
        }
    }
}

/// <summary>
/// Model statistics.
/// </summary>
public class ModelStatistics
{
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _totalInputTokens;
    private long _totalOutputTokens;
    private readonly Lock _lock = new();

    /// <summary>
    /// Provider name.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Total number of requests.
    /// </summary>
    public long TotalRequests
    {
        get
        {
            lock (_lock)
            {
                return _totalRequests;
            }
        }
    }

    /// <summary>
    /// Number of successful requests.
    /// </summary>
    public long SuccessfulRequests
    {
        get
        {
            lock (_lock)
            {
                return _successfulRequests;
            }
        }
    }

    /// <summary>
    /// Number of failed requests.
    /// </summary>
    public long FailedRequests
    {
        get
        {
            lock (_lock)
            {
                return _failedRequests;
            }
        }
    }

    /// <summary>
    /// Total number of input tokens.
    /// </summary>
    public long TotalInputTokens
    {
        get
        {
            lock (_lock)
            {
                return _totalInputTokens;
            }
        }
    }

    /// <summary>
    /// Total number of output tokens.
    /// </summary>
    public long TotalOutputTokens
    {
        get
        {
            lock (_lock)
            {
                return _totalOutputTokens;
            }
        }
    }

    private decimal _totalCost;
    private double _averageLatencyMs;
    private long _p99LatencyMs;
    private DateTimeOffset _lastUpdated = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total cost in USD.
    /// </summary>
    public decimal TotalCost
    {
        get
        {
            lock (_lock)
            {
                return _totalCost;
            }
        }
    }

    /// <summary>
    /// Average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _averageLatencyMs;
            }
        }
    }

    /// <summary>
    /// P99 latency in milliseconds.
    /// </summary>
    public long P99LatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _p99LatencyMs;
            }
        }
    }

    /// <summary>
    /// Last updated time.
    /// </summary>
    public DateTimeOffset LastUpdated
    {
        get
        {
            lock (_lock)
            {
                return _lastUpdated;
            }
        }
    }

    /// <summary>
    /// Success rate.
    /// </summary>
    public double SuccessRate
    {
        get
        {
            lock (_lock)
            {
                return _totalRequests == 0 ? 1.0 : (double)_successfulRequests / _totalRequests;
            }
        }
    }

    /// <summary>
    /// Whether the provider is healthy (success rate > 95%).
    /// </summary>
    public bool IsHealthy => SuccessRate >= 0.95;

    /// <summary>
    /// Records a successful request.
    /// </summary>
    public void RecordSuccess(long inputTokens, long outputTokens, decimal cost, double latencyMs)
    {
        lock (_lock)
        {
            _totalInputTokens += inputTokens;
            _totalOutputTokens += outputTokens;
            _totalRequests++;
            _successfulRequests++;
            var successCount = _successfulRequests;
            _totalCost += cost;
            _averageLatencyMs = (_averageLatencyMs * (successCount - 1) + latencyMs) / successCount;
            _lastUpdated = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Records a failed request.
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _totalRequests++;
            _failedRequests++;
            _lastUpdated = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the P99 latency.
    /// </summary>
    public void UpdateP99Latency(long p99Ms)
    {
        lock (_lock)
        {
            _p99LatencyMs = p99Ms;
        }
    }
}

/// <summary>
/// Model router configuration options.
/// </summary>
public class ModelRouterOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ModelRouter";

    /// <summary>
    /// Routing strategy.
    /// </summary>
    public ModelRoutingStrategy Strategy { get; set; } = ModelRoutingStrategy.CostOptimized;

    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Unhealthy threshold (consecutive failure count).
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Recovery threshold (consecutive success count).
    /// </summary>
    public int RecoveryThreshold { get; set; } = 2;

    /// <summary>
    /// Whether to enable failover.
    /// </summary>
    public bool EnableFailover { get; set; } = true;

    /// <summary>
    /// Maximum number of failover retries.
    /// </summary>
    public int MaxFailoverRetries { get; set; } = 2;

    /// <summary>
    /// Custom model pricing configuration.
    /// </summary>
    public Dictionary<string, ModelPricing> CustomPricing { get; set; } = new();

    /// <inheritdoc />
    public void Validate()
    {
        if (HealthCheckIntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "HealthCheckIntervalSeconds must be greater than 0"
            );
        }

        if (UnhealthyThreshold <= 0)
        {
            throw new InvalidOperationException("UnhealthyThreshold must be greater than 0");
        }

        if (RecoveryThreshold <= 0)
        {
            throw new InvalidOperationException("RecoveryThreshold must be greater than 0");
        }

        if (MaxFailoverRetries < 0)
        {
            throw new InvalidOperationException("MaxFailoverRetries must be non-negative");
        }
    }
}

/// <summary>
/// Model routing strategy.
/// </summary>
public enum ModelRoutingStrategy
{
    /// <summary>Cost optimized (select the cheapest model).</summary>
    CostOptimized,

    /// <summary>Latency optimized (select the fastest model).</summary>
    LatencyOptimized,

    /// <summary>Load balancing (round-robin distribution).</summary>
    RoundRobin,

    /// <summary>Weighted load balancing.</summary>
    WeightedRoundRobin,

    /// <summary>Random selection.</summary>
    Random,

    /// <summary>Priority (configured order with failover).</summary>
    Priority,
}
