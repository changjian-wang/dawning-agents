using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Pinecone;

namespace Dawning.Agents.Pinecone;

/// <summary>
/// Dependency injection extension methods for Pinecone services.
/// </summary>
public static class PineconeServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Pinecone vector store using configuration.
    /// </summary>
    /// <remarks>
    /// appsettings.json example:
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
        services
            .AddOptions<PineconeOptions>()
            .Configure(opts =>
            {
                configuration.GetSection(PineconeOptions.SectionName).Bind(opts);

                // Support environment variable override
                var envApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");
                if (!string.IsNullOrWhiteSpace(envApiKey))
                {
                    opts.ApiKey = envApiKey;
                }
            })
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(PineconeOptions)} configuration"
            )
            .ValidateOnStart();

        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;
            return new PineconeClient(opts.ApiKey);
        });
        services.TryAddSingleton<IVectorStore, PineconeVectorStore>();
        return services;
    }

    /// <summary>
    /// Adds the Pinecone vector store using a configuration delegate.
    /// </summary>
    public static IServiceCollection AddPineconeVectorStore(
        this IServiceCollection services,
        Action<PineconeOptions> configure
    )
    {
        services
            .AddOptions<PineconeOptions>()
            .Configure(configure)
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(PineconeOptions)} configuration"
            )
            .ValidateOnStart();
        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PineconeOptions>>().Value;
            return new PineconeClient(opts.ApiKey);
        });
        services.TryAddSingleton<IVectorStore, PineconeVectorStore>();
        return services;
    }

    /// <summary>
    /// Adds the Pinecone vector store with quick configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Pinecone API Key</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="namespace">The namespace (optional).</param>
    /// <param name="vectorSize">The vector dimension.</param>
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
    /// Adds a Pinecone Serverless vector store with automatic index creation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Pinecone API Key</param>
    /// <param name="indexName">The index name.</param>
    /// <param name="vectorSize">The vector dimension.</param>
    /// <param name="cloud">The cloud provider (aws, gcp, azure).</param>
    /// <param name="region">The region (e.g., us-east-1).</param>
    /// <param name="namespace">The namespace (optional).</param>
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
