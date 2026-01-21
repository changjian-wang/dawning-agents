using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// RAG 服务的 DI 扩展方法
/// </summary>
public static class RAGServiceCollectionExtensions
{
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
        services.AddSingleton<IEmbeddingProvider>(sp =>
            new SimpleEmbeddingProvider(
                dimensions,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<SimpleEmbeddingProvider>>()
            )
        );
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
}
