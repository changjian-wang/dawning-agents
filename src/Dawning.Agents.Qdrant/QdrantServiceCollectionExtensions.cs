using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Qdrant;

/// <summary>
/// Qdrant 服务的 DI 扩展方法
/// </summary>
public static class QdrantServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Qdrant 向量存储（使用配置）
    /// </summary>
    /// <remarks>
    /// appsettings.json 示例:
    /// <code>
    /// {
    ///   "Qdrant": {
    ///     "Host": "localhost",
    ///     "Port": 6334,
    ///     "CollectionName": "documents",
    ///     "VectorSize": 1536
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddQdrantVectorStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        services.TryAddSingleton<IVectorStore, QdrantVectorStore>();
        return services;
    }

    /// <summary>
    /// 添加 Qdrant 向量存储（使用配置委托）
    /// </summary>
    public static IServiceCollection AddQdrantVectorStore(
        this IServiceCollection services,
        Action<QdrantOptions> configure
    )
    {
        services.Configure(configure);
        services.TryAddSingleton<IVectorStore, QdrantVectorStore>();
        return services;
    }

    /// <summary>
    /// 添加 Qdrant 向量存储（快速配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="host">Qdrant 主机</param>
    /// <param name="port">gRPC 端口</param>
    /// <param name="collectionName">集合名称</param>
    /// <param name="vectorSize">向量维度</param>
    /// <param name="apiKey">API Key（可选）</param>
    public static IServiceCollection AddQdrantVectorStore(
        this IServiceCollection services,
        string host = "localhost",
        int port = 6334,
        string collectionName = "documents",
        int vectorSize = 1536,
        string? apiKey = null
    )
    {
        return services.AddQdrantVectorStore(options =>
        {
            options.Host = host;
            options.Port = port;
            options.CollectionName = collectionName;
            options.VectorSize = vectorSize;
            options.ApiKey = apiKey;
        });
    }

    /// <summary>
    /// 添加 Qdrant Cloud 向量存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="cloudUrl">Qdrant Cloud URL（如 xxx.aws.cloud.qdrant.io）</param>
    /// <param name="apiKey">Qdrant Cloud API Key</param>
    /// <param name="collectionName">集合名称</param>
    /// <param name="vectorSize">向量维度</param>
    public static IServiceCollection AddQdrantCloud(
        this IServiceCollection services,
        string cloudUrl,
        string apiKey,
        string collectionName = "documents",
        int vectorSize = 1536
    )
    {
        return services.AddQdrantVectorStore(options =>
        {
            options.Host = cloudUrl;
            options.Port = 6334;
            options.CollectionName = collectionName;
            options.VectorSize = vectorSize;
            options.ApiKey = apiKey;
            options.UseTls = true;
        });
    }
}
