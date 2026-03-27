using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.LLM;

/// <summary>
/// Dependency injection extensions for <see cref="ILLMProvider"/>.
/// </summary>
/// <remarks>
/// Supports Ollama (local LLM), OpenAI, and Azure OpenAI providers.
/// Automatically selects the provider based on the configured <see cref="LLMOptions.ProviderType"/>.
/// </remarks>
public static class LLMServiceCollectionExtensions
{
    /// <summary>
    /// The named <see cref="HttpClient"/> identifier used for Ollama.
    /// </summary>
    public const string OllamaHttpClientName = "Ollama";

    /// <summary>
    /// Registers the LLM provider services from an <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>Automatically selects the provider based on <see cref="LLMOptions.ProviderType"/>:</para>
    /// <list type="bullet">
    ///   <item>Ollama – local LLM</item>
    ///   <item>OpenAI - OpenAI API</item>
    ///   <item>AzureOpenAI - Azure OpenAI Service</item>
    /// </list>
    /// <para>
    /// appsettings.json example (Ollama):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "Ollama",
    ///     "Model": "qwen2.5:0.5b",
    ///     "Endpoint": "http://localhost:11434"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// appsettings.json example (OpenAI):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "OpenAI",
    ///     "Model": "gpt-4o-mini",
    ///     "ApiKey": "sk-xxx"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// appsettings.json example (Azure OpenAI):
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "AzureOpenAI",
    ///     "Endpoint": "https://your-resource.openai.azure.com",
    ///     "Model": "gpt-4o-deployment",
    ///     "ApiKey": "your-api-key"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Bind configuration
        services.AddValidatedOptions<LLMOptions>(configuration, LLMOptions.SectionName);

        // Fall back to environment variables when the configuration section is absent
        var section = configuration.GetSection(LLMOptions.SectionName);
        if (!section.Exists())
        {
            services.PostConfigure<LLMOptions>(ApplyEnvironmentVariables);
        }

        // Register HttpClient for Ollama
        RegisterOllamaHttpClient(services);

        // Register the provider (auto-selected by configuration)
        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return CreateProvider(sp, options);
        });

        return services;
    }

    /// <summary>
    /// Registers a hot-reloadable LLM provider that responds to configuration changes.
    /// </summary>
    /// <remarks>
    /// <para>The provider registered by this method automatically responds to configuration changes.</para>
    /// <para>When the LLM section in appsettings.json is modified, the provider is automatically rebuilt.</para>
    /// <para>
    /// Use cases:
    /// - Switching models at runtime
    /// - Dynamically adjusting parameters (e.g., Temperature)
    /// - Frequent configuration changes in development/test environments
    /// </para>
    /// <para>
    /// appsettings.json example:
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "Ollama",
    ///     "Model": "qwen2.5:0.5b",
    ///     "Endpoint": "http://localhost:11434"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHotReloadableLLMProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Bind configuration
        services.AddValidatedOptions<LLMOptions>(configuration, LLMOptions.SectionName);

        // Fall back to environment variables when the configuration section is absent
        var section = configuration.GetSection(LLMOptions.SectionName);
        if (!section.Exists())
        {
            services.PostConfigure<LLMOptions>(ApplyEnvironmentVariables);
        }

        // Register HttpClient for Ollama
        RegisterOllamaHttpClient(services);

        // Register the hot-reloadable provider
        services.TryAddSingleton<ILLMProvider, HotReloadableLLMProvider>();

        return services;
    }

    /// <summary>
    /// Registers the LLM provider services using a configuration delegate.
    /// </summary>
    public static IServiceCollection AddLLMProvider(
        this IServiceCollection services,
        Action<LLMOptions> configure
    )
    {
        services.AddValidatedOptions(configure);

        // Register HttpClient
        RegisterOllamaHttpClient(services);

        services.TryAddSingleton<ILLMProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            options.Validate();
            return CreateProvider(sp, options);
        });

        return services;
    }

    /// <summary>
    /// Registers the Ollama provider (local LLM).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The model name.</param>
    /// <param name="endpoint">The Ollama endpoint URL.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddOllamaProvider("qwen2.5:0.5b");
    /// </code>
    /// </example>
    public static IServiceCollection AddOllamaProvider(
        this IServiceCollection services,
        string model = "qwen2.5:0.5b",
        string endpoint = "http://localhost:11434"
    )
    {
        return services.AddLLMProvider(options =>
        {
            options.ProviderType = LLMProviderType.Ollama;
            options.Model = model;
            options.Endpoint = endpoint;
        });
    }

    private static void RegisterOllamaHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(
            OllamaHttpClientName,
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
                var endpoint = options.Endpoint ?? "http://localhost:11434";
                client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
                client.Timeout = TimeSpan.FromMinutes(5);
            }
        );
    }

    /// <summary>
    /// Creates the provider corresponding to the specified options.
    /// </summary>
    private static ILLMProvider CreateProvider(IServiceProvider sp, LLMOptions options)
    {
        var loggerFactory = sp.GetService<ILoggerFactory>();

        return options.ProviderType switch
        {
            LLMProviderType.Ollama => CreateOllamaProvider(sp, options),
            LLMProviderType.OpenAI => throw new NotSupportedException(
                "The OpenAI provider has been moved to a separate package. Install Dawning.Agents.OpenAI and call services.AddOpenAIProvider(apiKey, model)."
            ),
            LLMProviderType.AzureOpenAI => throw new NotSupportedException(
                "The Azure OpenAI provider has been moved to a separate package. Install Dawning.Agents.Azure and call services.AddAzureOpenAIProvider(endpoint, apiKey, deployment)."
            ),
            _ => throw new NotSupportedException(
                $"Unsupported provider type: {options.ProviderType}"
            ),
        };
    }

    private static OllamaProvider CreateOllamaProvider(IServiceProvider sp, LLMOptions options)
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(OllamaHttpClientName);
        var loggerFactory = sp.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<OllamaProvider>();

        return new OllamaProvider(httpClient, options.Model, logger);
    }

    /// <summary>
    /// Applies environment variables to the options.
    /// </summary>
    private static void ApplyEnvironmentVariables(LLMOptions options)
    {
        // OpenAI environment variables
        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openaiKey) && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ProviderType = LLMProviderType.OpenAI;
            options.ApiKey = openaiKey;
            options.Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
            return;
        }

        // Azure OpenAI environment variables
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(azureEndpoint) && !string.IsNullOrWhiteSpace(azureKey))
        {
            options.ProviderType = LLMProviderType.AzureOpenAI;
            options.Endpoint = azureEndpoint;
            options.ApiKey = azureKey;
            options.Model =
                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? options.Model;
            return;
        }

        // Ollama environment variables (default)
        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");

        options.Endpoint = ollamaEndpoint ?? options.Endpoint ?? "http://localhost:11434";
        options.Model = ollamaModel ?? options.Model ?? "qwen2.5:0.5b";
    }
}
