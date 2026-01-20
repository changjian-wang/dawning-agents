using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
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
        services.Configure<MemoryOptions>(configuration.GetSection(MemoryOptions.SectionName));

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
}
