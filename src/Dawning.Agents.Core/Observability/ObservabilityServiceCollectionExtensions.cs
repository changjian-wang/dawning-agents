namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// 可观测性 DI 扩展方法
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// 添加可观测性服务
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<TelemetryConfig>(
            configuration.GetSection(TelemetryConfig.SectionName)
        );

        services.TryAddSingleton<TelemetryConfig>(sp =>
        {
            var config = new TelemetryConfig();
            configuration.GetSection(TelemetryConfig.SectionName).Bind(config);
            return config;
        });

        services.TryAddSingleton<AgentTelemetry>();
        services.TryAddSingleton<MetricsCollector>();
        services.TryAddSingleton<AgentHealthCheck>();

        return services;
    }

    /// <summary>
    /// 添加可观测性服务（使用配置操作）
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        Action<TelemetryConfig>? configure = null
    )
    {
        var config = new TelemetryConfig();
        configure?.Invoke(config);

        services.TryAddSingleton(config);
        services.TryAddSingleton<AgentTelemetry>();
        services.TryAddSingleton<MetricsCollector>();
        services.TryAddSingleton<AgentHealthCheck>();

        return services;
    }

    /// <summary>
    /// 添加健康检查提供者
    /// </summary>
    public static IServiceCollection AddHealthCheckProvider<T>(this IServiceCollection services)
        where T : class, IHealthCheckProvider
    {
        services.AddSingleton<IHealthCheckProvider, T>();
        return services;
    }

    /// <summary>
    /// 包装 Agent 为可观测 Agent
    /// </summary>
    public static IServiceCollection AddObservableAgent<TAgent>(this IServiceCollection services)
        where TAgent : class, IAgent
    {
        services.AddSingleton<TAgent>();
        services.AddSingleton<IAgent>(sp =>
        {
            var innerAgent = sp.GetRequiredService<TAgent>();
            var telemetry = sp.GetRequiredService<AgentTelemetry>();
            var config = sp.GetRequiredService<TelemetryConfig>();
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(typeof(ObservableAgent).FullName ?? nameof(ObservableAgent));
            return new ObservableAgent(innerAgent, telemetry, config, logger);
        });

        return services;
    }

    /// <summary>
    /// 包装已注册的 Agent 为可观测 Agent
    /// </summary>
    public static IServiceCollection WrapAgentWithObservability(this IServiceCollection services)
    {
        // 查找并替换 IAgent 注册
        var agentDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgent));
        if (agentDescriptor != null)
        {
            services.Remove(agentDescriptor);

            // 重新注册为内部 Agent
            services.Add(
                new ServiceDescriptor(
                    typeof(InnerAgentMarker),
                    sp =>
                    {
                        // 根据原始注册创建实例
                        if (agentDescriptor.ImplementationInstance != null)
                        {
                            return new InnerAgentMarker(
                                (IAgent)agentDescriptor.ImplementationInstance
                            );
                        }
                        else if (agentDescriptor.ImplementationFactory != null)
                        {
                            return new InnerAgentMarker(
                                (IAgent)agentDescriptor.ImplementationFactory(sp)
                            );
                        }
                        else if (agentDescriptor.ImplementationType != null)
                        {
                            return new InnerAgentMarker(
                                (IAgent)
                                    ActivatorUtilities.CreateInstance(
                                        sp,
                                        agentDescriptor.ImplementationType
                                    )
                            );
                        }

                        throw new InvalidOperationException("无法创建内部 Agent");
                    },
                    agentDescriptor.Lifetime
                )
            );

            // 注册可观测 Agent
            services.AddSingleton<IAgent>(sp =>
            {
                var marker = sp.GetRequiredService<InnerAgentMarker>();
                var telemetry = sp.GetRequiredService<AgentTelemetry>();
                var config = sp.GetRequiredService<TelemetryConfig>();
                var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger(typeof(ObservableAgent).FullName ?? nameof(ObservableAgent));
                return new ObservableAgent(marker.Agent, telemetry, config, logger);
            });
        }

        return services;
    }

    /// <summary>
    /// 创建可观测 Agent 包装器
    /// </summary>
    public static ObservableAgent CreateObservableAgent(
        this IServiceProvider serviceProvider,
        IAgent innerAgent
    )
    {
        var telemetry = serviceProvider.GetRequiredService<AgentTelemetry>();
        var config = serviceProvider.GetRequiredService<TelemetryConfig>();
        var loggerFactory = serviceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger(typeof(ObservableAgent).FullName ?? nameof(ObservableAgent));
        return new ObservableAgent(innerAgent, telemetry, config, logger);
    }

    private class InnerAgentMarker
    {
        public IAgent Agent { get; }

        public InnerAgentMarker(IAgent agent)
        {
            Agent = agent;
        }
    }
}
