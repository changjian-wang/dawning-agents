using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Dependency injection extension methods for memory services.
/// </summary>
public static class MemoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds memory services (automatically selects the type based on configuration).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMemory(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<MemoryOptions>(configuration, MemoryOptions.SectionName);

        // Register token counter
        services.TryAddSingleton<ITokenCounter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MemoryOptions>>().Value;
            return new SimpleTokenCounter(options.ModelName, options.MaxContextTokens);
        });

        // Register memory based on configuration
        services.TryAddScoped<IConversationMemory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MemoryOptions>>().Value;
            var tokenCounter = sp.GetRequiredService<ITokenCounter>();

            return options.Type switch
            {
                MemoryType.Buffer => new BufferMemory(tokenCounter),
                MemoryType.Window => new WindowMemory(tokenCounter, options.WindowSize),
                MemoryType.Summary => new SummaryMemory(
                    sp.GetRequiredService<ILLMProvider>(),
                    tokenCounter,
                    options.MaxRecentMessages,
                    options.SummaryThreshold
                ),
                MemoryType.Adaptive => new AdaptiveMemory(
                    sp.GetRequiredService<ILLMProvider>(),
                    tokenCounter,
                    options.DowngradeThreshold,
                    options.MaxRecentMessages,
                    options.SummaryThreshold
                ),
                MemoryType.Vector => new VectorMemory(
                    sp.GetRequiredService<IVectorStore>(),
                    sp.GetRequiredService<IEmbeddingProvider>(),
                    tokenCounter,
                    options.MaxRecentMessages,
                    options.RetrieveTopK,
                    options.MinRelevanceScore
                ),
                _ => new BufferMemory(tokenCounter),
            };
        });

        return services;
    }

    /// <summary>
    /// Adds <see cref="BufferMemory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBufferMemory(
        this IServiceCollection services,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        services.TryAddScoped<IConversationMemory>(sp => new BufferMemory(
            sp.GetRequiredService<ITokenCounter>()
        ));

        return services;
    }

    /// <summary>
    /// Adds <see cref="WindowMemory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="windowSize">The window size.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWindowMemory(
        this IServiceCollection services,
        int windowSize = 10,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        services.TryAddScoped<IConversationMemory>(sp => new WindowMemory(
            sp.GetRequiredService<ITokenCounter>(),
            windowSize
        ));

        return services;
    }

    /// <summary>
    /// Adds <see cref="SummaryMemory"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="maxRecentMessages">The number of recent messages to retain.</param>
    /// <param name="summaryThreshold">The message count threshold that triggers summarization.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSummaryMemory(
        this IServiceCollection services,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        services.TryAddScoped<IConversationMemory>(sp => new SummaryMemory(
            sp.GetRequiredService<ILLMProvider>(),
            sp.GetRequiredService<ITokenCounter>(),
            maxRecentMessages,
            summaryThreshold
        ));

        return services;
    }

    /// <summary>
    /// Adds a token counter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTokenCounter(
        this IServiceCollection services,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        return services;
    }

    /// <summary>
    /// Adds <see cref="AdaptiveMemory"/> (automatic downgrade).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="downgradeThreshold">The token threshold that triggers downgrade. Defaults to 4000.</param>
    /// <param name="maxRecentMessages">The number of recent messages to retain after downgrade. Defaults to 6.</param>
    /// <param name="summaryThreshold">The message count threshold that triggers summarization after downgrade. Defaults to 10.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdaptiveMemory(
        this IServiceCollection services,
        int downgradeThreshold = 4000,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        services.TryAddScoped<IConversationMemory>(sp => new AdaptiveMemory(
            sp.GetRequiredService<ILLMProvider>(),
            sp.GetRequiredService<ITokenCounter>(),
            downgradeThreshold,
            maxRecentMessages,
            summaryThreshold
        ));

        return services;
    }

    /// <summary>
    /// Adds <see cref="VectorMemory"/> (vector retrieval-augmented).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="recentWindowSize">The number of recent messages to retain. Defaults to 6.</param>
    /// <param name="retrieveTopK">The number of relevant messages to retrieve. Defaults to 5.</param>
    /// <param name="minRelevanceScore">The minimum relevance score threshold. Defaults to 0.5.</param>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// Requires <see cref="IVectorStore"/> and <see cref="IEmbeddingProvider"/> to be registered first.
    /// </remarks>
    public static IServiceCollection AddVectorMemory(
        this IServiceCollection services,
        int recentWindowSize = 6,
        int retrieveTopK = 5,
        float minRelevanceScore = 0.5f,
        string modelName = "gpt-4",
        int maxContextTokens = 8192
    )
    {
        services.TryAddSingleton<ITokenCounter>(
            new SimpleTokenCounter(modelName, maxContextTokens)
        );
        services.TryAddScoped<IConversationMemory>(sp => new VectorMemory(
            sp.GetRequiredService<IVectorStore>(),
            sp.GetRequiredService<IEmbeddingProvider>(),
            sp.GetRequiredService<ITokenCounter>(),
            recentWindowSize,
            retrieveTopK,
            minRelevanceScore
        ));

        return services;
    }
}
