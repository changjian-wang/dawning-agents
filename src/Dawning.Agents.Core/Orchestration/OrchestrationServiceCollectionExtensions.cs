namespace Dawning.Agents.Core.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// 编排器 DI 扩展方法
/// </summary>
public static class OrchestrationServiceCollectionExtensions
{
    /// <summary>
    /// 添加编排器配置
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<OrchestratorOptions>(
            configuration.GetSection(OrchestratorOptions.SectionName)
        );

        return services;
    }

    /// <summary>
    /// 添加编排器配置（通过委托）
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        Action<OrchestratorOptions> configure
    )
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// 添加顺序编排器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">编排器名称</param>
    /// <param name="configureAgents">配置 Agent 列表</param>
    public static IServiceCollection AddSequentialOrchestrator(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, SequentialOrchestrator> configureAgents
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<SequentialOrchestrator>>();

            var orchestrator = new SequentialOrchestrator(name, options, logger);
            configureAgents(sp, orchestrator);

            return orchestrator;
        });

        return services;
    }

    /// <summary>
    /// 添加顺序编排器（使用指定的 Agent）
    /// </summary>
    public static IServiceCollection AddSequentialOrchestrator(
        this IServiceCollection services,
        string name,
        params IAgent[] agents
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<SequentialOrchestrator>>();

            var orchestrator = new SequentialOrchestrator(name, options, logger);
            orchestrator.AddAgents(agents);

            return orchestrator;
        });

        return services;
    }

    /// <summary>
    /// 添加并行编排器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">编排器名称</param>
    /// <param name="configureAgents">配置 Agent 列表</param>
    public static IServiceCollection AddParallelOrchestrator(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, ParallelOrchestrator> configureAgents
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<ParallelOrchestrator>>();

            var orchestrator = new ParallelOrchestrator(name, options, logger);
            configureAgents(sp, orchestrator);

            return orchestrator;
        });

        return services;
    }

    /// <summary>
    /// 添加并行编排器（使用指定的 Agent）
    /// </summary>
    public static IServiceCollection AddParallelOrchestrator(
        this IServiceCollection services,
        string name,
        params IAgent[] agents
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<ParallelOrchestrator>>();

            var orchestrator = new ParallelOrchestrator(name, options, logger);
            orchestrator.AddAgents(agents);

            return orchestrator;
        });

        return services;
    }

    /// <summary>
    /// 添加已注册的所有 IAgent 到编排器
    /// </summary>
    public static IServiceCollection AddSequentialOrchestratorWithAllAgents(
        this IServiceCollection services,
        string name
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<SequentialOrchestrator>>();
            var agents = sp.GetServices<IAgent>();

            var orchestrator = new SequentialOrchestrator(name, options, logger);
            orchestrator.AddAgents(agents);

            return orchestrator;
        });

        return services;
    }

    /// <summary>
    /// 添加已注册的所有 IAgent 到并行编排器
    /// </summary>
    public static IServiceCollection AddParallelOrchestratorWithAllAgents(
        this IServiceCollection services,
        string name
    )
    {
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<OrchestratorOptions>>();
            var logger =
                sp.GetService<Microsoft.Extensions.Logging.ILogger<ParallelOrchestrator>>();
            var agents = sp.GetServices<IAgent>();

            var orchestrator = new ParallelOrchestrator(name, options, logger);
            orchestrator.AddAgents(agents);

            return orchestrator;
        });

        return services;
    }
}
