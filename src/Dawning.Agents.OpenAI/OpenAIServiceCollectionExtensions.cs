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
