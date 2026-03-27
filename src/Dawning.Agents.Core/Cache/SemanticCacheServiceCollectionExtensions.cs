using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Cache;

/// <summary>
/// Dependency injection extension methods for semantic cache.
/// </summary>
public static class SemanticCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds semantic cache services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Requires <see cref="IVectorStore"/> and <see cref="IEmbeddingProvider"/> to be registered first.
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
    /// Adds semantic cache services with a custom configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
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
    /// Adds semantic cache services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="similarityThreshold">The similarity threshold (default 0.95).</param>
    /// <param name="maxEntries">The maximum number of entries (default 10000).</param>
    /// <param name="expirationMinutes">The expiration time in minutes (default 1440).</param>
    /// <returns>The service collection for chaining.</returns>
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
