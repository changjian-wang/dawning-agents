using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Memory 服务的 DI 扩展方法
/// </summary>
public static class MemoryServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Memory 服务（根据配置自动选择类型）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMemory(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<MemoryOptions>(configuration, MemoryOptions.SectionName);

        // 注册 Token 计数器
        services.TryAddSingleton<ITokenCounter>(sp =>
        {
            var options =
                configuration.GetSection(MemoryOptions.SectionName).Get<MemoryOptions>()
                ?? new MemoryOptions();
            return new SimpleTokenCounter(options.ModelName, options.MaxContextTokens);
        });

        // 根据配置注册 Memory
        services.TryAddScoped<IConversationMemory>(sp =>
        {
            var options =
                configuration.GetSection(MemoryOptions.SectionName).Get<MemoryOptions>()
                ?? new MemoryOptions();
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
    /// 添加 BufferMemory
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
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
    /// 添加 WindowMemory
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="windowSize">窗口大小</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
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
    /// 添加 SummaryMemory
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="maxRecentMessages">保留的最近消息数</param>
    /// <param name="summaryThreshold">触发摘要的消息数阈值</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
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
    /// 添加 Token 计数器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
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
    /// 添加 AdaptiveMemory（自动降级）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="downgradeThreshold">触发降级的 token 阈值（默认 4000）</param>
    /// <param name="maxRecentMessages">降级后保留的最近消息数（默认 6）</param>
    /// <param name="summaryThreshold">降级后触发摘要的消息数阈值（默认 10）</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
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
    /// 添加 VectorMemory（向量检索增强）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="recentWindowSize">保留的最近消息数（默认 6）</param>
    /// <param name="retrieveTopK">检索的相关消息数（默认 5）</param>
    /// <param name="minRelevanceScore">最小相关性分数（默认 0.5）</param>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// 使用前需要先注册 IVectorStore 和 IEmbeddingProvider
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
