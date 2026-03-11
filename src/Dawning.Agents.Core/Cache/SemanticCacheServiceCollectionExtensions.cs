using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Cache;

/// <summary>
/// 语义缓存的 DI 扩展方法
/// </summary>
public static class SemanticCacheServiceCollectionExtensions
{
    /// <summary>
    /// 添加语义缓存服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 使用前需要先注册 IVectorStore 和 IEmbeddingProvider
    /// </remarks>
    public static IServiceCollection AddSemanticCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<SemanticCacheOptions>(
            configuration,
            SemanticCacheOptions.SectionName
        );

        services.TryAddSingleton<ISemanticCache, SemanticCache>();

        return services;
    }

    /// <summary>
    /// 添加语义缓存服务（使用自定义配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSemanticCache(
        this IServiceCollection services,
        Action<SemanticCacheOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        services.TryAddSingleton<ISemanticCache, SemanticCache>();

        return services;
    }

    /// <summary>
    /// 添加语义缓存服务（使用默认配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="similarityThreshold">相似度阈值（默认 0.95）</param>
    /// <param name="maxEntries">最大条目数（默认 10000）</param>
    /// <param name="expirationMinutes">过期时间（分钟，默认 1440）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSemanticCache(
        this IServiceCollection services,
        float similarityThreshold = 0.95f,
        int maxEntries = 10000,
        int expirationMinutes = 1440
    )
    {
        services.AddValidatedOptions<SemanticCacheOptions>(options =>
        {
            options.SimilarityThreshold = similarityThreshold;
            options.MaxEntries = maxEntries;
            options.ExpirationMinutes = expirationMinutes;
        });

        services.TryAddSingleton<ISemanticCache, SemanticCache>();

        return services;
    }
}
