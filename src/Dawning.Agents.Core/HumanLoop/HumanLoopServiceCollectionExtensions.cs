using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// HumanLoop 服务的 DI 扩展方法
/// </summary>
public static class HumanLoopServiceCollectionExtensions
{
    /// <summary>
    /// 添加人机协作服务（使用自动审批处理器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHumanLoop(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<HumanLoopOptions>(
            configuration.GetSection(HumanLoopOptions.SectionName)
        );

        services.TryAddSingleton<ApprovalConfig>();
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// 添加人机协作服务（使用自动审批处理器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHumanLoop(
        this IServiceCollection services,
        Action<HumanLoopOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<HumanLoopOptions>(_ => { });
        }

        services.TryAddSingleton<ApprovalConfig>();
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// 添加自动审批处理器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAutoApprovalHandler(this IServiceCollection services)
    {
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        return services;
    }

    /// <summary>
    /// 添加异步回调处理器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAsyncCallbackHandler(this IServiceCollection services)
    {
        services.TryAddSingleton<AsyncCallbackHandler>();
        services.TryAddSingleton<IHumanInteractionHandler>(sp =>
            sp.GetRequiredService<AsyncCallbackHandler>()
        );
        return services;
    }

    /// <summary>
    /// 添加审批工作流
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">审批配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddApprovalWorkflow(
        this IServiceCollection services,
        Action<ApprovalConfig>? configure = null
    )
    {
        var config = new ApprovalConfig();
        configure?.Invoke(config);

        services.AddSingleton(config);
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// 添加人机协作 Agent 包装器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHumanInLoopAgent(
        this IServiceCollection services,
        Action<HumanLoopOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<HumanInLoopAgent>();

        return services;
    }
}
