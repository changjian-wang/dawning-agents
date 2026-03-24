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
    /// 添加 ReAct Agent 服务（基于文本解析的 Agent）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddReActAgent(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AgentOptions>(configuration, AgentOptions.SectionName);

        // 确保 ToolRegistry 已注册（Agent 可选依赖）
        services.AddToolRegistry();

        services.TryAddScoped<IAgent, ReActAgent>();

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
        services.AddValidatedOptions(configure);

        // 确保 ToolRegistry 已注册（Agent 可选依赖）
        services.AddToolRegistry();

        services.TryAddScoped<IAgent, ReActAgent>();

        return services;
    }

    /// <summary>
    /// 添加 Function Calling Agent 服务（基于 LLM 原生工具调用的 Agent）
    /// </summary>
    /// <remarks>
    /// <para>使用 LLM 原生 Function Calling，比 ReActAgent 的文本解析更可靠</para>
    /// <para>需要 LLM Provider 支持 Function Calling（OpenAI、Azure OpenAI、Ollama 等）</para>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddFunctionCallingAgent(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AgentOptions>(configuration, AgentOptions.SectionName);

        services.AddToolRegistry();

        services.TryAddScoped<IAgent, FunctionCallingAgent>();

        return services;
    }

    /// <summary>
    /// 添加 Function Calling Agent 服务（使用配置委托）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddFunctionCallingAgent(
        this IServiceCollection services,
        Action<AgentOptions> configure
    )
    {
        services.AddValidatedOptions(configure);

        services.AddToolRegistry();

        services.TryAddScoped<IAgent, FunctionCallingAgent>();

        return services;
    }

    /// <summary>
    /// 添加反思引擎（基于 LLM 的工具失败诊断与修复）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">反思配置（可选）</param>
    public static IServiceCollection AddReflectionEngine(
        this IServiceCollection services,
        Action<ReflectionOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<ReflectionOptions>(o => o.Enabled = true);
        }

        services.TryAddSingleton<IReflectionEngine, LLMReflectionEngine>();
        return services;
    }
}
