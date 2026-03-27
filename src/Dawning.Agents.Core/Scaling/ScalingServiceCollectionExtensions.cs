namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Configuration;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Scaling;
using Dawning.Agents.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scaling and configuration DI extension methods.
/// </summary>
public static class ScalingServiceCollectionExtensions
{
    /// <summary>
    /// Adds scaling configuration.
    /// </summary>
    public static IServiceCollection AddScaling(
        this IServiceCollection services,
        Action<ScalingOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        return services;
    }

    /// <summary>
    /// Adds scaling configuration from <see cref="IConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddScaling(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<ScalingOptions>(configuration, "Scaling");
        return services;
    }

    /// <summary>
    /// Adds the request queue.
    /// </summary>
    public static IServiceCollection AddAgentRequestQueue(
        this IServiceCollection services,
        int capacity = 1000
    )
    {
        services.TryAddSingleton<IAgentRequestQueue>(sp =>
        {
            var logger = sp.GetService<ILogger<AgentRequestQueue>>();
            return new AgentRequestQueue(capacity, logger);
        });
        return services;
    }

    /// <summary>
    /// Adds the load balancer.
    /// </summary>
    public static IServiceCollection AddAgentLoadBalancer(this IServiceCollection services)
    {
        services.TryAddSingleton<IAgentLoadBalancer>(sp =>
        {
            var logger = sp.GetService<ILogger<AgentLoadBalancer>>();
            return new AgentLoadBalancer(logger);
        });
        return services;
    }

    /// <summary>
    /// Adds the distributed load balancer with consistent hashing, weighted routing, and failover support.
    /// </summary>
    public static IServiceCollection AddDistributedLoadBalancer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<DistributedLoadBalancerOptions>(
            configuration.GetSection(DistributedLoadBalancerOptions.SectionName)
        );

        services.TryAddSingleton<DistributedLoadBalancer>(sp =>
        {
            var serviceRegistry = sp.GetService<IServiceRegistry>();
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<DistributedLoadBalancerOptions>>();
            var logger = sp.GetService<ILogger<DistributedLoadBalancer>>();
            return new DistributedLoadBalancer(serviceRegistry, options, logger);
        });

        services.TryAddSingleton<IAgentLoadBalancer>(sp =>
            sp.GetRequiredService<DistributedLoadBalancer>()
        );

        return services;
    }

    /// <summary>
    /// Adds the circuit breaker.
    /// </summary>
    public static IServiceCollection AddCircuitBreaker(
        this IServiceCollection services,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null
    )
    {
        services.TryAddSingleton<ICircuitBreaker>(sp =>
        {
            var logger = sp.GetService<ILogger<CircuitBreaker>>();
            return new CircuitBreaker(failureThreshold, resetTimeout, logger);
        });
        return services;
    }

    /// <summary>
    /// Adds the environment variable secrets manager.
    /// </summary>
    public static IServiceCollection AddEnvironmentSecretsManager(this IServiceCollection services)
    {
        services.TryAddSingleton<ISecretsManager>(sp =>
        {
            var logger = sp.GetService<ILogger<EnvironmentSecretsManager>>();
            return new EnvironmentSecretsManager(logger);
        });
        return services;
    }

    /// <summary>
    /// Adds the in-memory secrets manager.
    /// </summary>
    public static IServiceCollection AddInMemorySecretsManager(this IServiceCollection services)
    {
        services.TryAddSingleton<ISecretsManager>(sp =>
        {
            var logger = sp.GetService<ILogger<InMemorySecretsManager>>();
            return new InMemorySecretsManager(logger);
        });
        return services;
    }

    /// <summary>
    /// Adds deployment configuration.
    /// </summary>
    public static IServiceCollection AddDeploymentConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<AgentDeploymentOptions>(
            configuration,
            AgentDeploymentOptions.SectionName
        );
        services.AddValidatedOptions<LLMDeploymentOptions>(
            configuration,
            LLMDeploymentOptions.SectionName
        );
        services.AddValidatedOptions<CacheOptions>(configuration, CacheOptions.SectionName);
        services.AddValidatedOptions<ScalingOptions>(configuration, ScalingOptions.SectionName);
        return services;
    }

    /// <summary>
    /// Adds all production deployment services.
    /// </summary>
    public static IServiceCollection AddProductionDeployment(
        this IServiceCollection services,
        IConfiguration configuration,
        int queueCapacity = 1000,
        int circuitBreakerThreshold = 5
    )
    {
        // Configuration
        services.AddDeploymentConfiguration(configuration);

        // Secrets management
        services.AddEnvironmentSecretsManager();

        // Scaling components
        services.AddAgentRequestQueue(queueCapacity);
        services.AddAgentLoadBalancer();
        services.AddCircuitBreaker(circuitBreakerThreshold);

        return services;
    }
}
