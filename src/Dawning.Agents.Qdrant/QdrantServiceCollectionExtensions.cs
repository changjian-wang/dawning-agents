using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace Dawning.Agents.Qdrant;

/// <summary>
/// Dependency injection extension methods for Qdrant services.
/// </summary>
public static class QdrantServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Qdrant vector store using configuration.
    /// </summary>
    /// <remarks>
    /// appsettings.json example:
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
        services
            .AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(QdrantOptions)} configuration"
            )
            .ValidateOnStart();
        RegisterQdrantClient(services);
        services.TryAddSingleton<IVectorStore, QdrantVectorStore>();
        return services;
    }

    /// <summary>
    /// Adds the Qdrant vector store using a configuration delegate.
    /// </summary>
    public static IServiceCollection AddQdrantVectorStore(
        this IServiceCollection services,
        Action<QdrantOptions> configure
    )
    {
        services
            .AddOptions<QdrantOptions>()
            .Configure(configure)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(QdrantOptions)} configuration"
            )
            .ValidateOnStart();
        RegisterQdrantClient(services);
        services.TryAddSingleton<IVectorStore, QdrantVectorStore>();
        return services;
    }

    /// <summary>
    /// Adds the Qdrant vector store with quick configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="host">The Qdrant host.</param>
    /// <param name="port">The gRPC port.</param>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="vectorSize">The vector dimension.</param>
    /// <param name="apiKey">The API key (optional).</param>
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
    /// Adds the Qdrant Cloud vector store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cloudUrl">The Qdrant Cloud URL (e.g., xxx.aws.cloud.qdrant.io).</param>
    /// <param name="apiKey">The Qdrant Cloud API key.</param>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="vectorSize">The vector dimension.</param>
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

    private static void RegisterQdrantClient(IServiceCollection services)
    {
        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            return !string.IsNullOrWhiteSpace(opts.ApiKey)
                ? new QdrantClient(opts.Host, opts.Port, https: opts.UseTls, apiKey: opts.ApiKey)
                : new QdrantClient(opts.Host, opts.Port, https: opts.UseTls);
        });
    }
}
