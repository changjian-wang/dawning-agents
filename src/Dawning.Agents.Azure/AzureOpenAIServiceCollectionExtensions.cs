using Azure.Core;
using Dawning.Agents.Abstractions;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.Azure;

/// <summary>
/// Dependency injection extensions for Azure OpenAI provider.
/// </summary>
public static class AzureOpenAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers an Azure OpenAI provider with API key authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpoint">The Azure OpenAI endpoint URL.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="deploymentName">The deployment name.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(
    ///     "https://your-resource.openai.azure.com/",
    ///     "your-api-key",
    ///     "gpt-4o"
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        services.TryAddSingleton<ILLMProvider>(sp => new AzureOpenAIProvider(
            endpoint,
            apiKey,
            deploymentName,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// Registers an Azure OpenAI provider with Azure AD authentication.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpoint">The Azure OpenAI endpoint URL.</param>
    /// <param name="credential">The Azure credential.</param>
    /// <param name="deploymentName">The deployment name.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(
    ///     "https://your-resource.openai.azure.com/",
    ///     new DefaultAzureCredential(),
    ///     "gpt-4o"
    /// );
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        string endpoint,
        TokenCredential credential,
        string deploymentName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        services.TryAddSingleton<ILLMProvider>(sp => new AzureOpenAIProvider(
            endpoint,
            credential,
            deploymentName,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIProvider>()
        ));

        return services;
    }

    /// <summary>
    /// Registers an Azure OpenAI provider using a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(options =>
    /// {
    ///     options.Endpoint = "https://your-resource.openai.azure.com/";
    ///     options.ApiKey = "your-api-key";
    ///     options.DeploymentName = "gpt-4o";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        Action<AzureOpenAIProviderOptions> configure
    )
    {
        var options = new AzureOpenAIProviderOptions();
        configure(options);
        options.Validate();

        return services.AddAzureOpenAIProvider(
            options.Endpoint!,
            options.ApiKey!,
            options.DeploymentName!
        );
    }

    /// <summary>
    /// Registers an Azure OpenAI provider using an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section containing Endpoint, ApiKey, and DeploymentName.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddAzureOpenAIProvider(configuration.GetSection("AzureOpenAI"));
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureOpenAIProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new AzureOpenAIProviderOptions
        {
            Endpoint = configuration[nameof(AzureOpenAIProviderOptions.Endpoint)],
            ApiKey = configuration[nameof(AzureOpenAIProviderOptions.ApiKey)],
            DeploymentName = configuration[nameof(AzureOpenAIProviderOptions.DeploymentName)],
        };
        options.Validate();

        return services.AddAzureOpenAIProvider(
            options.Endpoint!,
            options.ApiKey!,
            options.DeploymentName!
        );
    }

    /// <summary>
    /// Registers an Azure OpenAI embedding provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="endpoint">The Azure OpenAI endpoint URL.</param>
    /// <param name="apiKey">The Azure OpenAI API key.</param>
    /// <param name="deploymentName">The embedding model deployment name.</param>
    /// <param name="dimensions">The vector dimensions.</param>
    public static IServiceCollection AddAzureOpenAIEmbedding(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName,
        int dimensions = 1536
    )
    {
        services.AddSingleton<IEmbeddingProvider>(sp => new AzureOpenAIEmbeddingProvider(
            endpoint,
            apiKey,
            deploymentName,
            dimensions,
            sp.GetService<ILoggerFactory>()?.CreateLogger<AzureOpenAIEmbeddingProvider>()
        ));
        return services;
    }
}

/// <summary>
/// Configuration options for the Azure OpenAI provider.
/// </summary>
public class AzureOpenAIProviderOptions : IValidatableOptions
{
    /// <summary>
    /// Gets or sets the Azure OpenAI endpoint URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// API Key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the deployment name.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Azure OpenAI Endpoint is required");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(DeploymentName))
        {
            throw new InvalidOperationException("Azure OpenAI DeploymentName is required");
        }
    }
}
