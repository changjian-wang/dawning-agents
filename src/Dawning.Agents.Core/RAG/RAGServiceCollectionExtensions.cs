using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Dependency injection extension methods for RAG services.
/// </summary>
public static class RAGServiceCollectionExtensions
{
    /// <summary>
    /// The named HttpClient identifier for Ollama embedding.
    /// </summary>
    public const string OllamaEmbeddingHttpClientName = "OllamaEmbedding";

    /// <summary>
    /// Adds RAG services with in-memory vector store and simple embedding.
    /// </summary>
    /// <remarks>
    /// Suitable for development and testing scenarios.
    /// Production environments should use an external vector database and LLM embedding API.
    /// </remarks>
    public static IServiceCollection AddRAG(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmbeddingProvider, SimpleEmbeddingProvider>();
        services.TryAddSingleton<IVectorStore, InMemoryVectorStore>();
        services.TryAddSingleton<DocumentChunker>();
        services.TryAddSingleton<IRetriever, VectorRetriever>();
        services.TryAddSingleton<KnowledgeBase>();

        return services;
    }

    /// <summary>
    /// Adds RAG services with the specified configuration.
    /// </summary>
    public static IServiceCollection AddRAG(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<RAGOptions>(configuration, RAGOptions.SectionName);
        return services.AddRAG();
    }

    /// <summary>
    /// Adds RAG services with a custom configuration delegate.
    /// </summary>
    public static IServiceCollection AddRAG(
        this IServiceCollection services,
        Action<RAGOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        return services.AddRAG();
    }

    /// <summary>
    /// Adds an embedding provider selected automatically based on LLM configuration.
    /// </summary>
    /// <remarks>
    /// <para>Automatically selects an embedding provider based on <see cref="LLMOptions.ProviderType"/>:</para>
    /// <list type="bullet">
    ///   <item>Ollama - OllamaEmbeddingProvider</item>
    ///   <item>OpenAI - OpenAIEmbeddingProvider</item>
    ///   <item>AzureOpenAI - AzureOpenAIEmbeddingProvider</item>
    /// </list>
    /// <para>
    /// appsettings.json example:
    /// <code>
    /// {
    ///   "LLM": {
    ///     "ProviderType": "OpenAI",
    ///     "ApiKey": "sk-xxx"
    ///   },
    ///   "RAG": {
    ///     "EmbeddingModel": "text-embedding-3-small"
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddEmbeddingProvider(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Bind configuration
        services.AddValidatedOptions<LLMOptions>(configuration, LLMOptions.SectionName);
        services.AddValidatedOptions<RAGOptions>(configuration, RAGOptions.SectionName);

        // Register HttpClient for Ollama
        RegisterOllamaEmbeddingHttpClient(services);

        // Register embedding provider
        services.TryAddSingleton<IEmbeddingProvider>(sp =>
        {
            var llmOptions = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            var ragOptions = sp.GetRequiredService<IOptions<RAGOptions>>().Value;

            return CreateEmbeddingProvider(sp, llmOptions, ragOptions);
        });

        return services;
    }

    /// <summary>
    /// Adds the Ollama embedding provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="model">The embedding model name.</param>
    /// <param name="endpoint">The Ollama endpoint URL.</param>
    public static IServiceCollection AddOllamaEmbedding(
        this IServiceCollection services,
        string model = "nomic-embed-text",
        string endpoint = "http://localhost:11434"
    )
    {
        services.AddHttpClient(
            OllamaEmbeddingHttpClientName,
            client =>
            {
                client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
                client.Timeout = TimeSpan.FromMinutes(5);
            }
        );

        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(OllamaEmbeddingHttpClientName);
            var logger = sp.GetService<ILogger<OllamaEmbeddingProvider>>();
            return new OllamaEmbeddingProvider(httpClient, model, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds the in-memory vector store.
    /// </summary>
    public static IServiceCollection AddInMemoryVectorStore(this IServiceCollection services)
    {
        services.TryAddSingleton<IVectorStore, InMemoryVectorStore>();
        return services;
    }

    /// <summary>
    /// Adds the simple embedding provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dimensions">The vector dimensions (default: 384).</param>
    public static IServiceCollection AddSimpleEmbedding(
        this IServiceCollection services,
        int dimensions = 384
    )
    {
        services.AddSingleton<IEmbeddingProvider>(sp => new SimpleEmbeddingProvider(
            dimensions,
            sp.GetService<ILogger<SimpleEmbeddingProvider>>()
        ));
        return services;
    }

    /// <summary>
    /// Adds the knowledge base.
    /// </summary>
    public static IServiceCollection AddKnowledgeBase(this IServiceCollection services)
    {
        services.TryAddSingleton<DocumentChunker>();
        services.TryAddSingleton<KnowledgeBase>();
        return services;
    }

    #region Private Helpers

    private static void RegisterOllamaEmbeddingHttpClient(IServiceCollection services)
    {
        services.AddHttpClient(
            OllamaEmbeddingHttpClientName,
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
                var endpoint = options.Endpoint ?? "http://localhost:11434";
                client.BaseAddress = new Uri(endpoint.TrimEnd('/'));
                client.Timeout = TimeSpan.FromMinutes(5);
            }
        );
    }

    private static IEmbeddingProvider CreateEmbeddingProvider(
        IServiceProvider sp,
        LLMOptions llmOptions,
        RAGOptions ragOptions
    )
    {
        var loggerFactory = sp.GetService<ILoggerFactory>();
        var model = ragOptions.EmbeddingModel;

        return llmOptions.ProviderType switch
        {
            LLMProviderType.Ollama => CreateOllamaEmbeddingProvider(sp, model, loggerFactory),
            LLMProviderType.OpenAI => throw new NotSupportedException(
                "OpenAI embedding has been moved to a separate package. Install Dawning.Agents.OpenAI and call services.AddOpenAIEmbedding(apiKey, model)."
            ),
            LLMProviderType.AzureOpenAI => throw new NotSupportedException(
                "Azure OpenAI embedding has been moved to a separate package. Install Dawning.Agents.Azure and call services.AddAzureOpenAIEmbedding(endpoint, apiKey, deployment)."
            ),
            _ => throw new NotSupportedException(
                $"Unsupported provider type: {llmOptions.ProviderType}"
            ),
        };
    }

    private static OllamaEmbeddingProvider CreateOllamaEmbeddingProvider(
        IServiceProvider sp,
        string model,
        ILoggerFactory? loggerFactory
    )
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(OllamaEmbeddingHttpClientName);
        var logger = loggerFactory?.CreateLogger<OllamaEmbeddingProvider>();
        return new OllamaEmbeddingProvider(httpClient, model, logger);
    }

    #endregion
}
