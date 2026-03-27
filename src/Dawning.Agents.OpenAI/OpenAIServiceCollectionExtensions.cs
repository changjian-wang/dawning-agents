using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// Dependency injection extensions for OpenAI provider.
/// </summary>
public static class OpenAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers an OpenAI provider with the specified API key and model.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model name. Defaults to <c>gpt-4o</c>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAIProvider("sk-xxx", "gpt-4o");
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        services.TryAddSingleton<ILLMProvider>(sp => new OpenAIProvider(
            apiKey,
            model,
            sp.GetService<ILoggerFactory>()?.CreateLogger<OpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// Registers an OpenAI provider using a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAIProvider(options =>
    /// {
    ///     options.ApiKey = "sk-xxx";
    ///     options.Model = "gpt-4o";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        Action<OpenAIProviderOptions> configure
    )
    {
        var options = new OpenAIProviderOptions();
        configure(options);
        options.Validate();

        return services.AddOpenAIProvider(options.ApiKey!, options.Model);
    }

    /// <summary>
    /// Registers an OpenAI provider using an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing ApiKey and Model.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAIProvider(configuration.GetSection("OpenAI"));
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAIProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new OpenAIProviderOptions
        {
            ApiKey = configuration[nameof(OpenAIProviderOptions.ApiKey)],
            Model = configuration[nameof(OpenAIProviderOptions.Model)] ?? "gpt-4o",
        };
        options.Validate();

        return services.AddOpenAIProvider(options.ApiKey!, options.Model);
    }

    /// <summary>
    /// Registers an OpenAI-compatible provider with a custom endpoint.
    /// Supports DeepSeek, Zhipu (GLM), Moonshot (Kimi), Baichuan, Qwen API, and other
    /// providers that expose an OpenAI-compatible chat completions API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="model">The model name (e.g. <c>deepseek-chat</c>, <c>glm-4</c>, <c>moonshot-v1-8k</c>).</param>
    /// <param name="endpoint">The base URL (e.g. <c>https://api.deepseek.com</c>).</param>
    /// <param name="providerName">A display name for logging. Defaults to <c>OpenAICompatible</c>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // DeepSeek
    /// services.AddOpenAICompatibleProvider("sk-xxx", "deepseek-chat", "https://api.deepseek.com");
    ///
    /// // Zhipu (GLM)
    /// services.AddOpenAICompatibleProvider("xxx.yyy", "glm-4", "https://open.bigmodel.cn/api/paas/v4");
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAICompatibleProvider(
        this IServiceCollection services,
        string apiKey,
        string model,
        string endpoint,
        string providerName = "OpenAICompatible"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        services.TryAddSingleton<ILLMProvider>(sp => new OpenAIProvider(
            apiKey,
            model,
            endpoint,
            providerName,
            sp.GetService<ILoggerFactory>()?.CreateLogger<OpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// Registers an OpenAI-compatible provider using a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOpenAICompatibleProvider(options =>
    /// {
    ///     options.ApiKey = "sk-xxx";
    ///     options.Model = "deepseek-chat";
    ///     options.Endpoint = "https://api.deepseek.com";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAICompatibleProvider(
        this IServiceCollection services,
        Action<OpenAICompatibleProviderOptions> configure
    )
    {
        var options = new OpenAICompatibleProviderOptions();
        configure(options);
        options.Validate();

        return services.AddOpenAICompatibleProvider(
            options.ApiKey!,
            options.Model,
            options.Endpoint!,
            options.ProviderName
        );
    }

    /// <summary>
    /// Registers an OpenAI-compatible provider using an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// Configuration:
    /// <code>
    /// {
    ///   "DeepSeek": {
    ///     "ApiKey": "sk-xxx",
    ///     "Model": "deepseek-chat",
    ///     "Endpoint": "https://api.deepseek.com"
    ///   }
    /// }
    /// </code>
    /// Registration:
    /// <code>
    /// services.AddOpenAICompatibleProvider(configuration.GetSection("DeepSeek"));
    /// </code>
    /// </example>
    public static IServiceCollection AddOpenAICompatibleProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new OpenAICompatibleProviderOptions
        {
            ApiKey = configuration[nameof(OpenAICompatibleProviderOptions.ApiKey)],
            Model = configuration[nameof(OpenAICompatibleProviderOptions.Model)] ?? "deepseek-chat",
            Endpoint = configuration[nameof(OpenAICompatibleProviderOptions.Endpoint)],
            ProviderName =
                configuration[nameof(OpenAICompatibleProviderOptions.ProviderName)]
                ?? "OpenAICompatible",
        };
        options.Validate();

        return services.AddOpenAICompatibleProvider(
            options.ApiKey!,
            options.Model,
            options.Endpoint!,
            options.ProviderName
        );
    }

    /// <summary>
    /// Registers an OpenAI-compatible embedding provider with a custom endpoint.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="model">The embedding model name.</param>
    /// <param name="endpoint">The base URL of the OpenAI-compatible API.</param>
    public static IServiceCollection AddOpenAICompatibleEmbedding(
        this IServiceCollection services,
        string apiKey,
        string model,
        string endpoint
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        services.TryAddSingleton<IEmbeddingProvider>(sp => new OpenAIEmbeddingProvider(
            apiKey,
            model,
            endpoint,
            sp.GetService<ILoggerFactory>()?.CreateLogger<OpenAIEmbeddingProvider>()
        ));
        return services;
    }

    /// <summary>
    /// Registers an OpenAI embedding provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The embedding model name.</param>
    public static IServiceCollection AddOpenAIEmbedding(
        this IServiceCollection services,
        string apiKey,
        string model = "text-embedding-3-small"
    )
    {
        services.TryAddSingleton<IEmbeddingProvider>(sp => new OpenAIEmbeddingProvider(
            apiKey,
            model,
            sp.GetService<ILoggerFactory>()?.CreateLogger<OpenAIEmbeddingProvider>()
        ));
        return services;
    }
}

/// <summary>
/// Configuration options for the OpenAI provider.
/// </summary>
public class OpenAIProviderOptions : IValidatableOptions
{
    /// <summary>
    /// Gets or sets the OpenAI API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("OpenAI ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("OpenAI Model is required");
        }
    }
}

/// <summary>
/// Configuration options for OpenAI-compatible providers (DeepSeek, Zhipu, Moonshot, etc.).
/// </summary>
public class OpenAICompatibleProviderOptions : IValidatableOptions
{
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>
    /// Gets or sets the base URL of the OpenAI-compatible API.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the display name for logging.
    /// </summary>
    public string ProviderName { get; set; } = "OpenAICompatible";

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("OpenAI-compatible ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("OpenAI-compatible Model is required");
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("OpenAI-compatible Endpoint is required");
        }
    }
}
