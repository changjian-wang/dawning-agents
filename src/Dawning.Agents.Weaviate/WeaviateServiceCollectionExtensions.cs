using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Weaviate;

/// <summary>
/// Weaviate 向量存储 DI 扩展方法
/// </summary>
public static class WeaviateServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Weaviate 向量存储（通过配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddWeaviateVectorStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<WeaviateOptions>(configuration.GetSection(WeaviateOptions.SectionName));

        services.AddHttpClient<WeaviateVectorStore>();
        services.TryAddSingleton<IVectorStore, WeaviateVectorStore>();

        return services;
    }

    /// <summary>
    /// 添加 Weaviate 向量存储（通过委托配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddWeaviateVectorStore(
        this IServiceCollection services,
        Action<WeaviateOptions> configure
    )
    {
        services.Configure(configure);

        services.AddHttpClient<WeaviateVectorStore>();
        services.TryAddSingleton<IVectorStore, WeaviateVectorStore>();

        return services;
    }
}
