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
/// 扩展和配置相关的 DI 扩展方法
/// </summary>
public static class ScalingServiceCollectionExtensions
{
    /// <summary>
    /// 添加扩展配置
    /// </summary>
    public static IServiceCollection AddScaling(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ScalingOptions>(configuration.GetSection(ScalingOptions.SectionName));
        return services;
    }

    /// <summary>
    /// 添加扩展配置
    /// </summary>
    public static IServiceCollection AddScaling(
        this IServiceCollection services,
        Action<ScalingOptions> configure
    )
    {
        services.Configure(configure);
        return services;
    }

    /// <summary>
    /// 添加请求队列
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
    /// 添加负载均衡器
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
    /// 添加分布式负载均衡器（支持一致性哈希、权重路由、故障转移）
    /// </summary>
    public static IServiceCollection AddDistributedLoadBalancer(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<DistributedLoadBalancerOptions>(
            configuration.GetSection(DistributedLoadBalancerOptions.SectionName)
        );

        services.TryAddSingleton<IAgentLoadBalancer>(sp =>
        {
            var serviceRegistry = sp.GetService<IServiceRegistry>();
            var options =
                sp.GetService<Microsoft.Extensions.Options.IOptions<DistributedLoadBalancerOptions>>();
            var logger = sp.GetService<ILogger<DistributedLoadBalancer>>();
            return new DistributedLoadBalancer(serviceRegistry, options, logger);
        });

        // 同时注册具体类型，便于访问扩展方法
        services.TryAddSingleton<DistributedLoadBalancer>(sp =>
            (DistributedLoadBalancer)sp.GetRequiredService<IAgentLoadBalancer>()
        );

        return services;
    }

    /// <summary>
    /// 添加熔断器
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
    /// 添加环境变量密钥管理器
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
    /// 添加内存密钥管理器
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
    /// 添加部署配置
    /// </summary>
    public static IServiceCollection AddDeploymentConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<AgentDeploymentOptions>(
            configuration.GetSection(AgentDeploymentOptions.SectionName)
        );
        services.Configure<LLMDeploymentOptions>(
            configuration.GetSection(LLMDeploymentOptions.SectionName)
        );
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<ScalingOptions>(configuration.GetSection(ScalingOptions.SectionName));
        return services;
    }

    /// <summary>
    /// 添加所有生产部署服务
    /// </summary>
    public static IServiceCollection AddProductionDeployment(
        this IServiceCollection services,
        IConfiguration configuration,
        int queueCapacity = 1000,
        int circuitBreakerThreshold = 5
    )
    {
        // 配置
        services.AddDeploymentConfiguration(configuration);

        // 密钥管理
        services.AddEnvironmentSecretsManager();

        // 扩展组件
        services.AddAgentRequestQueue(queueCapacity);
        services.AddAgentLoadBalancer();
        services.AddCircuitBreaker(circuitBreakerThreshold);

        return services;
    }
}
