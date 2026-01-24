namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// Agent 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Agent": {
///     "Name": "DawningAgent",
///     "MaxIterations": 10,
///     "MaxTokensPerRequest": 4000,
///     "RequestTimeout": "00:05:00",
///     "EnableSafetyGuardrails": true
///   }
/// }
/// </code>
/// </remarks>
public record AgentDeploymentOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string Name { get; init; } = "DefaultAgent";

    /// <summary>
    /// 最大迭代次数
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// 每个请求的最大 Token 数
    /// </summary>
    public int MaxTokensPerRequest { get; init; } = 4000;

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 是否启用安全护栏
    /// </summary>
    public bool EnableSafetyGuardrails { get; init; } = true;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Agent Name is required");
        }

        if (MaxIterations < 1)
        {
            throw new InvalidOperationException("MaxIterations must be at least 1");
        }

        if (MaxTokensPerRequest < 1)
        {
            throw new InvalidOperationException("MaxTokensPerRequest must be at least 1");
        }

        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("RequestTimeout must be positive");
        }
    }
}

/// <summary>
/// LLM 提供者配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "LLM": {
///     "Provider": "OpenAI",
///     "Model": "gpt-4",
///     "Temperature": 0.7,
///     "MaxRetries": 3
///   }
/// }
/// </code>
/// </remarks>
public record LLMDeploymentOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "LLM";

    /// <summary>
    /// LLM 提供者名称
    /// </summary>
    public string Provider { get; init; } = "OpenAI";

    /// <summary>
    /// API 密钥
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// 自定义端点
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; init; } = "gpt-4";

    /// <summary>
    /// 温度参数
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// 重试延迟
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new InvalidOperationException("Provider is required");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Model is required");
        }

        if (Temperature < 0 || Temperature > 2)
        {
            throw new InvalidOperationException("Temperature must be between 0 and 2");
        }

        if (MaxRetries < 0)
        {
            throw new InvalidOperationException("MaxRetries must be non-negative");
        }
    }
}

/// <summary>
/// 缓存配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Cache": {
///     "Enabled": true,
///     "Provider": "Redis",
///     "DefaultExpiration": "01:00:00"
///   }
/// }
/// </code>
/// </remarks>
public record CacheOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 缓存提供者
    /// </summary>
    public string Provider { get; init; } = "Memory";

    /// <summary>
    /// Redis 连接字符串
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// 默认过期时间
    /// </summary>
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// 最大缓存大小
    /// </summary>
    public int MaxCacheSize { get; init; } = 10000;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new InvalidOperationException("Cache Provider is required");
        }

        if (DefaultExpiration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("DefaultExpiration must be positive");
        }

        if (MaxCacheSize < 1)
        {
            throw new InvalidOperationException("MaxCacheSize must be at least 1");
        }
    }
}
