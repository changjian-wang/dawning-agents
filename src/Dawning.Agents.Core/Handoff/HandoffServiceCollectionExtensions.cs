using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Handoff;

/// <summary>
/// Handoff 服务注册扩展方法
/// </summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Handoff 处理器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHandoff(
        this IServiceCollection services,
        IConfiguration? configuration = null
    )
    {
        if (configuration != null)
        {
            services.Configure<HandoffOptions>(
                configuration.GetSection(HandoffOptions.SectionName)
            );
        }

        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        return services;
    }

    /// <summary>
    /// 添加 Handoff 处理器并配置选项
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHandoff(
        this IServiceCollection services,
        Action<HandoffOptions> configureOptions
    )
    {
        services.Configure(configureOptions);
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        return services;
    }

    /// <summary>
    /// 将 Agent 注册到 Handoff 系统
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="agent">要注册的 Agent</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAgentToHandoff(
        this IServiceCollection services,
        IAgent agent
    )
    {
        // 确保 Handoff 服务已注册
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        // 添加一个后处理器来注册 Agent
        services.AddSingleton<IHandoffAgentRegistration>(
            new HandoffAgentRegistration(agent)
        );

        return services;
    }

    /// <summary>
    /// 将 Agent 注册到 Handoff 系统（通过工厂方法）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="agentFactory">Agent 工厂方法</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAgentToHandoff(
        this IServiceCollection services,
        Func<IServiceProvider, IAgent> agentFactory
    )
    {
        // 确保 Handoff 服务已注册
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        // 添加一个后处理器来注册 Agent
        services.AddSingleton<IHandoffAgentRegistration>(sp =>
            new HandoffAgentRegistration(agentFactory(sp))
        );

        return services;
    }

    /// <summary>
    /// 确保所有标记的 Agent 已注册到 Handoff 系统
    /// </summary>
    /// <param name="services">服务提供者</param>
    public static void EnsureHandoffAgentsRegistered(this IServiceProvider services)
    {
        var handler = services.GetService<IHandoffHandler>();
        if (handler == null)
        {
            return;
        }

        var registrations = services.GetServices<IHandoffAgentRegistration>();
        foreach (var registration in registrations)
        {
            handler.RegisterAgent(registration.Agent);
        }
    }
}

/// <summary>
/// Handoff Agent 注册标记接口
/// </summary>
public interface IHandoffAgentRegistration
{
    /// <summary>
    /// 要注册的 Agent
    /// </summary>
    IAgent Agent { get; }
}

/// <summary>
/// Handoff Agent 注册实现
/// </summary>
internal class HandoffAgentRegistration : IHandoffAgentRegistration
{
    public HandoffAgentRegistration(IAgent agent)
    {
        Agent = agent;
    }

    public IAgent Agent { get; }
}
