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
        return (inputTokens * InputPricePerKToken / 1000m) +
               (outputTokens * OutputPricePerKToken / 1000m);
    }

    /// <summary>
    /// 预定义的模型定价（2026 年数据）
    /// </summary>
    public static class KnownPricing
    {
        // OpenAI 模型
        public static readonly ModelPricing Gpt4o = new()
        {
            Model = "gpt-4o",
            InputPricePerKToken = 0.0025m,
            OutputPricePerKToken = 0.01m
        };

        public static readonly ModelPricing Gpt4oMini = new()
        {
            Model = "gpt-4o-mini",
            InputPricePerKToken = 0.00015m,
            OutputPricePerKToken = 0.0006m
        };

        public static readonly ModelPricing Gpt4Turbo = new()
        {
            Model = "gpt-4-turbo",
            InputPricePerKToken = 0.01m,
            OutputPricePerKToken = 0.03m
        };

        public static readonly ModelPricing Gpt35Turbo = new()
        {
            Model = "gpt-3.5-turbo",
            InputPricePerKToken = 0.0005m,
            OutputPricePerKToken = 0.0015m
        };

        // Claude 模型
        public static readonly ModelPricing Claude3Opus = new()
        {
            Model = "claude-3-opus",
            InputPricePerKToken = 0.015m,
            OutputPricePerKToken = 0.075m
        };

        public static readonly ModelPricing Claude3Sonnet = new()
        {
            Model = "claude-3-sonnet",
            InputPricePerKToken = 0.003m,
            OutputPricePerKToken = 0.015m
        };

        public static readonly ModelPricing Claude3Haiku = new()
        {
            Model = "claude-3-haiku",
            InputPricePerKToken = 0.00025m,
            OutputPricePerKToken = 0.00125m
        };

        // 本地模型（免费）
        public static readonly ModelPricing Ollama = new()
        {
            Model = "ollama",
            InputPricePerKToken = 0m,
            OutputPricePerKToken = 0m
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
                    InputPricePerKToken = 0.001m,  // 默认估算
                    OutputPricePerKToken = 0.002m
                }
            };
        }
    }
}

/// <summary>
/// 模型统计信息
/// </summary>
public class ModelStatistics
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 总请求数
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// 成功请求数
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// 失败请求数
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// 总输入 Token 数
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// 总输出 Token 数
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// 总成本（美元）
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 平均延迟（毫秒）
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// P99 延迟（毫秒）
    /// </summary>
    public long P99LatencyMs { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate =>
        TotalRequests == 0 ? 1.0 : (double)SuccessfulRequests / TotalRequests;

    /// <summary>
    /// 是否健康（成功率 > 95%）
    /// </summary>
    public bool IsHealthy => SuccessRate >= 0.95;
}

/// <summary>
/// 模型路由器配置选项
/// </summary>
public class ModelRouterOptions
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
    Priority
}
