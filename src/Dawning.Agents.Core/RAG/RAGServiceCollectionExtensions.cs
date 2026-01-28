using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Azure;
using Dawning.Agents.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// RAG 服务的 DI 扩展方法
/// </summary>
public static class RAGServiceCollectionExtensions
{
    /// <summary>
    /// Ollama Embedding HttpClient 名称
    /// </summary>
    public const string OllamaEmbeddingHttpClientName = "OllamaEmbedding";

    /// <summary>
    /// 添加 RAG 服务（使用内存向量存储和简单嵌入）
    /// </summary>
    /// <remarks>
    /// 适用于开发和测试场景。
    /// 生产环境应使用外部向量数据库和 LLM Embedding API。
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
    /// 添加 RAG 服务（使用配置）
    /// </summary>
    public static IServiceCollection AddRAG(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<RAGOptions>(configuration.GetSection(RAGOptions.SectionName));
        return services.AddRAG();
    }

    /// <summary>
    /// 添加 RAG 服务（使用自定义配置）
    /// </summary>
    public static IServiceCollection AddRAG(
        this IServiceCollection services,
        Action<RAGOptions> configure
    )
    {
        services.Configure(configure);
        return services.AddRAG();
    }

    /// <summary>
    /// 添加 Embedding Provider（根据 LLM 配置自动选择）
    /// </summary>
    /// <remarks>
    /// <para>根据 LLMOptions.ProviderType 自动选择 Embedding Provider:</para>
    /// <list type="bullet">
    ///   <item>Ollama - OllamaEmbeddingProvider</item>
    ///   <item>OpenAI - OpenAIEmbeddingProvider</item>
    ///   <item>AzureOpenAI - AzureOpenAIEmbeddingProvider</item>
    /// </list>
    /// <para>
    /// appsettings.json 示例:
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
        // 绑定配置
        services.Configure<LLMOptions>(configuration.GetSection(LLMOptions.SectionName));
        services.Configure<RAGOptions>(configuration.GetSection(RAGOptions.SectionName));

        // 注册 HttpClient（用于 Ollama）
        RegisterOllamaEmbeddingHttpClient(services);

        // 注册 Embedding Provider
        services.TryAddSingleton<IEmbeddingProvider>(sp =>
        {
            var llmOptions = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
            var ragOptions = sp.GetRequiredService<IOptions<RAGOptions>>().Value;

            return CreateEmbeddingProvider(sp, llmOptions, ragOptions);
        });

        return services;
    }

    /// <summary>
    /// 添加 OpenAI Embedding Provider
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">嵌入模型名称</param>
    public static IServiceCollection AddOpenAIEmbedding(
        this IServiceCollection services,
        string apiKey,
        string model = "text-embedding-3-small"
    )
    {
        services.AddSingleton<IEmbeddingProvider>(
            new OpenAIEmbeddingProvider(apiKey, model)
        );
        return services;
    }

    /// <summary>
    /// 添加 Azure OpenAI Embedding Provider
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="endpoint">Azure OpenAI 端点</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">嵌入模型部署名称</param>
    /// <param name="dimensions">向量维度</param>
    public static IServiceCollection AddAzureOpenAIEmbedding(
        this IServiceCollection services,
        string endpoint,
        string apiKey,
        string deploymentName,
        int dimensions = 1536
    )
    {
        services.AddSingleton<IEmbeddingProvider>(
            new AzureOpenAIEmbeddingProvider(endpoint, apiKey, deploymentName, dimensions)
        );
        return services;
    }

    /// <summary>
    /// 添加 Ollama Embedding Provider
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="model">嵌入模型名称</param>
    /// <param name="endpoint">Ollama 端点</param>
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
    /// 添加内存向量存储
    /// </summary>
    public static IServiceCollection AddInMemoryVectorStore(this IServiceCollection services)
    {
        services.TryAddSingleton<IVectorStore, InMemoryVectorStore>();
        return services;
    }

    /// <summary>
    /// 添加简单嵌入提供者
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="dimensions">向量维度（默认 384）</param>
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
    /// 添加知识库
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
            LLMProviderType.OpenAI => new OpenAIEmbeddingProvider(llmOptions.ApiKey!, model),
            LLMProviderType.AzureOpenAI => new AzureOpenAIEmbeddingProvider(
                llmOptions.Endpoint!,
                llmOptions.ApiKey!,
                model
            ),
            _ => throw new NotSupportedException($"不支持的 Provider 类型: {llmOptions.ProviderType}"),
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
