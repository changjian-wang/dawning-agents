using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Weaviate;

/// <summary>
/// Dependency injection extension methods for Weaviate vector store.
/// </summary>
public static class WeaviateServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Weaviate vector store using configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWeaviateVectorStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<WeaviateOptions>()
            .Bind(configuration.GetSection(WeaviateOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(WeaviateOptions)} configuration"
            )
            .ValidateOnStart();

        services
            .AddHttpClient(nameof(WeaviateVectorStore))
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;
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
            var client = factory.CreateClient(nameof(WeaviateVectorStore));
            var options = sp.GetRequiredService<IOptions<WeaviateOptions>>();
            var logger = sp.GetService<ILogger<WeaviateVectorStore>>();
            return new WeaviateVectorStore(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds the Weaviate vector store using a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWeaviateVectorStore(
        this IServiceCollection services,
        Action<WeaviateOptions> configure
    )
    {
        services
            .AddOptions<WeaviateOptions>()
            .Configure(configure)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(WeaviateOptions)} configuration"
            )
            .ValidateOnStart();

        services
            .AddHttpClient(nameof(WeaviateVectorStore))
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;
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
            var client = factory.CreateClient(nameof(WeaviateVectorStore));
            var options = sp.GetRequiredService<IOptions<WeaviateOptions>>();
            var logger = sp.GetService<ILogger<WeaviateVectorStore>>();
            return new WeaviateVectorStore(client, options, logger);
        });

        return services;
    }
}
