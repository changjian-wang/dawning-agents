using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Chroma;

/// <summary>
/// Chroma 向量存储 DI 扩展
/// </summary>
public static class ChromaServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Chroma 向量存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ChromaOptions>(configuration.GetSection(ChromaOptions.SectionName));

        services
            .AddHttpClient<ChromaVectorStore>()
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<ChromaOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    if (!string.IsNullOrEmpty(options.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add(
                            "Authorization",
                            $"Bearer {options.ApiKey}"
                        );
                    }
                }
            );

        services.TryAddSingleton<IVectorStore, ChromaVectorStore>();

        return services;
    }

    /// <summary>
    /// 添加 Chroma 向量存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        Action<ChromaOptions> configureOptions
    )
    {
        services.Configure(configureOptions);

        services
            .AddHttpClient<ChromaVectorStore>()
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<ChromaOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    if (!string.IsNullOrEmpty(options.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add(
                            "Authorization",
                            $"Bearer {options.ApiKey}"
                        );
                    }
                }
            );

        services.TryAddSingleton<IVectorStore, ChromaVectorStore>();

        return services;
    }
}
