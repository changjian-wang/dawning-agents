using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 模型定价信息
/// </summary>
/// <remarks>
/// 价格单位：美元/1K tokens
/// 数据来源：各 LLM 提供商官方定价
/// </remarks>
public class ModelPricing
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// 输入价格（美元/1K tokens）
    /// </summary>
    public decimal InputPricePerKToken { get; init; }

    /// <summary>
    /// 输出价格（美元/1K tokens）
    /// </summary>
    public decimal OutputPricePerKToken { get; init; }

    /// <summary>
    /// 计算总成本
    /// </summary>
    public decimal CalculateCost(int inputTokens, int outputTokens)
    {
        return (inputTokens * InputPricePerKToken / 1000m)
            + (outputTokens * OutputPricePerKToken / 1000m);
    }

    /// <summary>
    /// 预定义的模型定价（2026 年数据）
    /// </summary>
    public static class KnownPricing
    {
        // OpenAI 模型

        /// <summary>GPT-4o 定价</summary>
        public static readonly ModelPricing Gpt4o = new()
        {
            Model = "gpt-4o",
            InputPricePerKToken = 0.0025m,
            OutputPricePerKToken = 0.01m,
        };

        /// <summary>GPT-4o Mini 定价</summary>
        public static readonly ModelPricing Gpt4oMini = new()
        {
            Model = "gpt-4o-mini",
            InputPricePerKToken = 0.00015m,
            OutputPricePerKToken = 0.0006m,
        };

        /// <summary>GPT-4 Turbo 定价</summary>
        public static readonly ModelPricing Gpt4Turbo = new()
        {
            Model = "gpt-4-turbo",
            InputPricePerKToken = 0.01m,
            OutputPricePerKToken = 0.03m,
        };

        /// <summary>GPT-3.5 Turbo 定价</summary>
        public static readonly ModelPricing Gpt35Turbo = new()
        {
            Model = "gpt-3.5-turbo",
            InputPricePerKToken = 0.0005m,
            OutputPricePerKToken = 0.0015m,
        };

        // Claude 模型

        /// <summary>Claude 3 Opus 定价</summary>
        public static readonly ModelPricing Claude3Opus = new()
        {
            Model = "claude-3-opus",
            InputPricePerKToken = 0.015m,
            OutputPricePerKToken = 0.075m,
        };

        /// <summary>Claude 3 Sonnet 定价</summary>
        public static readonly ModelPricing Claude3Sonnet = new()
        {
            Model = "claude-3-sonnet",
            InputPricePerKToken = 0.003m,
            OutputPricePerKToken = 0.015m,
        };

        /// <summary>Claude 3 Haiku 定价</summary>
        public static readonly ModelPricing Claude3Haiku = new()
        {
            Model = "claude-3-haiku",
            InputPricePerKToken = 0.00025m,
            OutputPricePerKToken = 0.00125m,
        };

        // 本地模型（免费）

        /// <summary>Ollama 本地模型定价（免费）</summary>
        public static readonly ModelPricing Ollama = new()
        {
            Model = "ollama",
            InputPricePerKToken = 0m,
            OutputPricePerKToken = 0m,
        };

        /// <summary>
        /// 根据模型名称获取定价
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
                    InputPricePerKToken = 0.001m, // 默认估算
                    OutputPricePerKToken = 0.002m,
                },
            };
        }
    }
}

/// <summary>
/// 模型统计信息
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
    /// 提供者名称
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    /// <summary>
    /// 成功请求数
    /// </summary>
    public long SuccessfulRequests => Interlocked.Read(ref _successfulRequests);

    /// <summary>
    /// 失败请求数
    /// </summary>
    public long FailedRequests => Interlocked.Read(ref _failedRequests);

    /// <summary>
    /// 总输入 Token 数
    /// </summary>
    public long TotalInputTokens => Interlocked.Read(ref _totalInputTokens);

    /// <summary>
    /// 总输出 Token 数
    /// </summary>
    public long TotalOutputTokens => Interlocked.Read(ref _totalOutputTokens);

    /// <summary>
    /// 总成本（美元）
    /// </summary>
    public decimal TotalCost { get; private set; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AverageLatencyMs { get; private set; }

    /// <summary>
    /// P99 延迟（毫秒）
    /// </summary>
    public long P99LatencyMs { get; private set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = Interlocked.Read(ref _totalRequests);
            var successful = Interlocked.Read(ref _successfulRequests);
            return total == 0 ? 1.0 : (double)successful / total;
        }
    }

    /// <summary>
    /// 是否健康（成功率 > 95%）
    /// </summary>
    public bool IsHealthy => SuccessRate >= 0.95;

    /// <summary>
    /// 记录成功请求
    /// </summary>
    public void RecordSuccess(long inputTokens, long outputTokens, decimal cost, double latencyMs)
    {
        Interlocked.Increment(ref _totalRequests);
        var successCount = Interlocked.Increment(ref _successfulRequests);
        Interlocked.Add(ref _totalInputTokens, inputTokens);
        Interlocked.Add(ref _totalOutputTokens, outputTokens);
        lock (_lock)
        {
            TotalCost += cost;
            AverageLatencyMs = (AverageLatencyMs * (successCount - 1) + latencyMs) / successCount;
            LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 记录失败请求
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _failedRequests);
        lock (_lock)
        {
            LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// 更新 P99 延迟
    /// </summary>
    public void UpdateP99Latency(long p99Ms)
    {
        lock (_lock)
        {
            P99LatencyMs = p99Ms;
        }
    }
}

/// <summary>
/// 模型路由器配置选项
/// </summary>
public class ModelRouterOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "ModelRouter";

    /// <summary>
    /// 路由策略
    /// </summary>
    public ModelRoutingStrategy Strategy { get; set; } = ModelRoutingStrategy.CostOptimized;

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 不健康阈值（连续失败次数）
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// 恢复阈值（连续成功次数）
    /// </summary>
    public int RecoveryThreshold { get; set; } = 2;

    /// <summary>
    /// 是否启用故障转移
    /// </summary>
    public bool EnableFailover { get; set; } = true;

    /// <summary>
    /// 故障转移最大重试次数
    /// </summary>
    public int MaxFailoverRetries { get; set; } = 2;

    /// <summary>
    /// 自定义模型定价配置
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
/// 模型路由策略
/// </summary>
public enum ModelRoutingStrategy
{
    /// <summary>成本优化（选择最便宜的模型）</summary>
    CostOptimized,

    /// <summary>延迟优化（选择响应最快的模型）</summary>
    LatencyOptimized,

    /// <summary>负载均衡（轮询分配）</summary>
    RoundRobin,

    /// <summary>加权负载均衡</summary>
    WeightedRoundRobin,

    /// <summary>随机选择</summary>
    Random,

    /// <summary>优先级（按配置顺序，故障转移）</summary>
    Priority,
}
