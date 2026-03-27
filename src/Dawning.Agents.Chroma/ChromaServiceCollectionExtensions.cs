using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Chroma;

/// <summary>
/// Dependency injection extension methods for Chroma vector store.
/// </summary>
public static class ChromaServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Chroma vector store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
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
    /// Adds the Chroma vector store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration delegate.</param>
    /// <returns>The service collection.</returns>
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
