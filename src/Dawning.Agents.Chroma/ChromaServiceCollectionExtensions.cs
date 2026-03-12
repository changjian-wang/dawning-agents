using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
        services
            .AddOptions<ChromaOptions>()
            .Bind(configuration.GetSection(ChromaOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(ChromaOptions)} configuration"
            )
            .ValidateOnStart();

        services
            .AddHttpClient(nameof(ChromaVectorStore))
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

        services.TryAddSingleton<IVectorStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(nameof(ChromaVectorStore));
            var options = sp.GetRequiredService<IOptions<ChromaOptions>>();
            var logger = sp.GetService<ILogger<ChromaVectorStore>>();
            return new ChromaVectorStore(client, options, logger);
        });

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
        services
            .AddOptions<ChromaOptions>()
            .Configure(configureOptions)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(ChromaOptions)} configuration"
            )
            .ValidateOnStart();

        services
            .AddHttpClient(nameof(ChromaVectorStore))
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

        services.TryAddSingleton<IVectorStore>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(nameof(ChromaVectorStore));
            var options = sp.GetRequiredService<IOptions<ChromaOptions>>();
            var logger = sp.GetService<ILogger<ChromaVectorStore>>();
            return new ChromaVectorStore(client, options, logger);
        });

        return services;
    }
}
