using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Core.Agent;
using Dawning.Agents.Core.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// Agent 服务注册扩展
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// 添加 ReAct Agent 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReActAgent(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));

        // 确保 ToolRegistry 已注册（Agent 可选依赖）
        services.AddToolRegistry();

        services.TryAddSingleton<IAgent, ReActAgent>();

        return services;
    }

    /// <summary>
    /// 添加 ReAct Agent 服务（使用配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReActAgent(
        this IServiceCollection services,
        Action<AgentOptions> configure
    )
    {
        services.Configure(configure);

        // 确保 ToolRegistry 已注册（Agent 可选依赖）
        services.AddToolRegistry();

        services.TryAddSingleton<IAgent, ReActAgent>();

        return services;
    }
}
