namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency injection extension methods for observability services.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds observability services to the service collection.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<TelemetryConfig>(configuration.GetSection(TelemetryConfig.SectionName));
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TelemetryConfig>>().Value);

        services.TryAddSingleton<AgentTelemetry>();
        services.TryAddSingleton<MetricsCollector>();
        services.TryAddSingleton<AgentHealthCheck>();

        return services;
    }

    /// <summary>
    /// Adds observability services to the service collection using a configuration action.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        Action<TelemetryConfig>? configure = null
    )
    {
        services.Configure(configure ?? (_ => { }));
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<TelemetryConfig>>().Value);
        services.TryAddSingleton<AgentTelemetry>();
        services.TryAddSingleton<MetricsCollector>();
        services.TryAddSingleton<AgentHealthCheck>();

        return services;
    }

    /// <summary>
    /// Adds a health check provider.
    /// </summary>
    public static IServiceCollection AddHealthCheckProvider<T>(this IServiceCollection services)
        where T : class, IHealthCheckProvider
    {
        services.AddSingleton<IHealthCheckProvider, T>();
        return services;
    }

    /// <summary>
    /// Wraps an agent as an observable agent.
    /// </summary>
    public static IServiceCollection AddObservableAgent<TAgent>(this IServiceCollection services)
        where TAgent : class, IAgent
    {
        services.AddScoped<TAgent>();
        services.AddScoped<IAgent>(sp =>
        {
            var innerAgent = sp.GetRequiredService<TAgent>();
            var telemetry = sp.GetRequiredService<AgentTelemetry>();
            var config = sp.GetRequiredService<TelemetryConfig>();
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(
                typeof(ObservableAgent).FullName ?? nameof(ObservableAgent)
            );
            return new ObservableAgent(innerAgent, telemetry, config, logger);
        });

        return services;
    }

    /// <summary>
    /// Wraps the registered agent with observability.
    /// </summary>
    public static IServiceCollection WrapAgentWithObservability(this IServiceCollection services)
    {
        // Find and replace IAgent registration
        var agentDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAgent));
        if (agentDescriptor != null)
        {
            services.Remove(agentDescriptor);

            // Re-register as inner agent
            services.Add(
                new ServiceDescriptor(
                    typeof(InnerAgentMarker),
                    sp =>
                    {
                        // Create instance based on original registration
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

                        throw new InvalidOperationException("Unable to create inner agent.");
                    },
                    agentDescriptor.Lifetime
                )
            );

            // Register observable agent (preserving original lifetime)
            services.Add(
                new ServiceDescriptor(
                    typeof(IAgent),
                    sp =>
                    {
                        var marker = sp.GetRequiredService<InnerAgentMarker>();
                        var telemetry = sp.GetRequiredService<AgentTelemetry>();
                        var config = sp.GetRequiredService<TelemetryConfig>();
                        var loggerFactory =
                            sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
                        var logger = loggerFactory?.CreateLogger(
                            typeof(ObservableAgent).FullName ?? nameof(ObservableAgent)
                        );
                        return new ObservableAgent(marker.Agent, telemetry, config, logger);
                    },
                    agentDescriptor.Lifetime
                )
            );
        }

        return services;
    }

    /// <summary>
    /// Creates an observable agent wrapper.
    /// </summary>
    public static ObservableAgent CreateObservableAgent(
        this IServiceProvider serviceProvider,
        IAgent innerAgent
    )
    {
        var telemetry = serviceProvider.GetRequiredService<AgentTelemetry>();
        var config = serviceProvider.GetRequiredService<TelemetryConfig>();
        var loggerFactory =
            serviceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger(
            typeof(ObservableAgent).FullName ?? nameof(ObservableAgent)
        );
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
