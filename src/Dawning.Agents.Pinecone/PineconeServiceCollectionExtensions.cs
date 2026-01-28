using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Pinecone;

/// <summary>
/// Pinecone 服务的 DI 扩展方法
/// </summary>
public static class PineconeServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Pinecone 向量存储（使用配置）
    /// </summary>
    /// <remarks>
    /// appsettings.json 示例:
    /// <code>
    /// {
    ///   "Pinecone": {
    ///     "ApiKey": "your-api-key",
    ///     "IndexName": "documents",
    ///     "Namespace": "default",
    ///     "VectorSize": 1536
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddPineconeVectorStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<PineconeOptions>(opts =>
        {
            configuration.GetSection(PineconeOptions.SectionName).Bind(opts);

            // 支持环境变量覆盖
            var envApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");
            if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                opts.ApiKey = envApiKey;
            }
        });

        services.TryAddSingleton<IVectorStore, PineconeVectorStore>();
        return services;
    }

    /// <summary>
    /// 添加 Pinecone 向量存储（使用配置委托）
    /// </summary>
    public static IServiceCollection AddPineconeVectorStore(
        this IServiceCollection services,
        Action<PineconeOptions> configure
    )
    {
        services.Configure(configure);
        services.TryAddSingleton<IVectorStore, PineconeVectorStore>();
        return services;
    }

    /// <summary>
    /// 添加 Pinecone 向量存储（快速配置）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">Pinecone API Key</param>
    /// <param name="indexName">索引名称</param>
    /// <param name="namespace">命名空间（可选）</param>
    /// <param name="vectorSize">向量维度</param>
    public static IServiceCollection AddPineconeVectorStore(
        this IServiceCollection services,
        string apiKey,
        string indexName = "documents",
        string? @namespace = null,
        int vectorSize = 1536
    )
    {
        return services.AddPineconeVectorStore(options =>
        {
            options.ApiKey = apiKey;
            options.IndexName = indexName;
            options.Namespace = @namespace;
            options.VectorSize = vectorSize;
        });
    }

    /// <summary>
    /// 添加 Pinecone Serverless 向量存储（自动创建索引）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="apiKey">Pinecone API Key</param>
    /// <param name="indexName">索引名称</param>
    /// <param name="vectorSize">向量维度</param>
    /// <param name="cloud">云提供商（aws, gcp, azure）</param>
    /// <param name="region">区域（如 us-east-1）</param>
    /// <param name="namespace">命名空间（可选）</param>
    public static IServiceCollection AddPineconeServerless(
        this IServiceCollection services,
        string apiKey,
        string indexName,
        int vectorSize = 1536,
        string cloud = "aws",
        string region = "us-east-1",
        string? @namespace = null
    )
    {
        return services.AddPineconeVectorStore(options =>
        {
            options.ApiKey = apiKey;
            options.IndexName = indexName;
            options.VectorSize = vectorSize;
            options.Namespace = @namespace;
            options.AutoCreateIndex = true;
            options.Cloud = cloud;
            options.Region = region;
        });
    }
}
