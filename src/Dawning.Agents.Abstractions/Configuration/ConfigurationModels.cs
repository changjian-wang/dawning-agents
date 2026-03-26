using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// Agent configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public record AgentDeploymentOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Agent name.
    /// </summary>
    public string Name { get; init; } = "DefaultAgent";

    /// <summary>
    /// Maximum iteration count
    /// </summary>
    public int MaxIterations { get; init; } = 10;

    /// <summary>
    /// Maximum tokens per request.
    /// </summary>
    public int MaxTokensPerRequest { get; init; } = 4000;

    /// <summary>
    /// Request timeout duration.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable safety guardrails.
    /// </summary>
    public bool EnableSafetyGuardrails { get; init; } = true;

    /// <summary>
    /// Validates the configuration.
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
/// LLM provider configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public record LLMDeploymentOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "LLM";

    /// <summary>
    /// LLM provider name.
    /// </summary>
    public string Provider { get; init; } = "OpenAI";

    /// <summary>
    /// API key.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Custom endpoint.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Model name.
    /// </summary>
    public string Model { get; init; } = "gpt-4";

    /// <summary>
    /// Temperature parameter.
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Retry delay.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Validates the configuration.
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

        if (RetryDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("RetryDelay must be positive");
        }
    }
}

/// <summary>
/// Cache configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public record CacheOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Whether caching is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Cache provider.
    /// </summary>
    public string Provider { get; init; } = "Memory";

    /// <summary>
    /// Redis connection string.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Default expiration time.
    /// </summary>
    public TimeSpan DefaultExpiration { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum cache size.
    /// </summary>
    public int MaxCacheSize { get; init; } = 10000;

    /// <summary>
    /// Validates the configuration.
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

        if (
            string.Equals(Provider, "Redis", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(ConnectionString)
        )
        {
            throw new InvalidOperationException(
                "ConnectionString is required when Provider is Redis"
            );
        }
    }
}
