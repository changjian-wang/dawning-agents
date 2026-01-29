using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 模型路由 DI 扩展方法
/// </summary>
public static class ModelRouterServiceCollectionExtensions
{
    /// <summary>
    /// 添加模型路由器（使用配置）
    /// </summary>
    /// <remarks>
    /// appsettings.json 示例:
    /// <code>
    /// {
    ///   "ModelRouter": {
    ///     "Strategy": "CostOptimized",
    ///     "EnableFailover": true,
    ///     "MaxFailoverRetries": 2,
    ///     "HealthCheckIntervalSeconds": 30
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddModelRouter(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ModelRouterOptions>(
            configuration.GetSection(ModelRouterOptions.SectionName)
        );

        return services.AddModelRouterCore();
    }

    /// <summary>
    /// 添加模型路由器（使用配置委托）
    /// </summary>
    public static IServiceCollection AddModelRouter(
        this IServiceCollection services,
        Action<ModelRouterOptions> configure
    )
    {
        services.Configure(configure);
        return services.AddModelRouterCore();
    }

    /// <summary>
    /// 添加模型路由器（使用指定策略）
    /// </summary>
    public static IServiceCollection AddModelRouter(
        this IServiceCollection services,
        ModelRoutingStrategy strategy = ModelRoutingStrategy.CostOptimized,
        bool enableFailover = true,
        int maxFailoverRetries = 2
    )
    {
        return services.AddModelRouter(options =>
        {
            options.Strategy = strategy;
            options.EnableFailover = enableFailover;
            options.MaxFailoverRetries = maxFailoverRetries;
        });
    }

    /// <summary>
    /// 添加成本优化路由器
    /// </summary>
    public static IServiceCollection AddCostOptimizedRouter(this IServiceCollection services)
    {
        return services.AddModelRouter(ModelRoutingStrategy.CostOptimized);
    }

    /// <summary>
    /// 添加延迟优化路由器
    /// </summary>
    public static IServiceCollection AddLatencyOptimizedRouter(this IServiceCollection services)
    {
        return services.AddModelRouter(ModelRoutingStrategy.LatencyOptimized);
    }

    /// <summary>
    /// 添加负载均衡路由器
    /// </summary>
    public static IServiceCollection AddLoadBalancedRouter(
        this IServiceCollection services,
        ModelRoutingStrategy strategy = ModelRoutingStrategy.RoundRobin
    )
    {
        return services.AddModelRouter(strategy);
    }

    private static IServiceCollection AddModelRouterCore(this IServiceCollection services)
    {
        // 注册路由器工厂
        services.TryAddSingleton<IModelRouter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModelRouterOptions>>();
            var providers = sp.GetServices<ILLMProvider>().ToList();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return options.Value.Strategy switch
            {
                ModelRoutingStrategy.CostOptimized =>
                    new CostOptimizedRouter(
                        providers,
                        options,
                        loggerFactory.CreateLogger<CostOptimizedRouter>()
                    ),
                ModelRoutingStrategy.LatencyOptimized =>
                    new LatencyOptimizedRouter(
                        providers,
                        options,
                        loggerFactory.CreateLogger<LatencyOptimizedRouter>()
                    ),
                ModelRoutingStrategy.RoundRobin or
                ModelRoutingStrategy.WeightedRoundRobin or
                ModelRoutingStrategy.Random =>
                    new LoadBalancedRouter(
                        providers,
                        options,
                        loggerFactory.CreateLogger<LoadBalancedRouter>()
                    ),
                ModelRoutingStrategy.Priority =>
                    new CostOptimizedRouter(
                        providers,
                        options,
                        loggerFactory.CreateLogger<CostOptimizedRouter>()
                    ),
                _ => new CostOptimizedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<CostOptimizedRouter>()
                )
            };
        });

        // 注册 RoutingLLMProvider
        services.TryAddSingleton<RoutingLLMProvider>();

        return services;
    }

    /// <summary>
    /// 添加多个 LLM 提供者用于路由
    /// </summary>
    public static IServiceCollection AddLLMProviders(
        this IServiceCollection services,
        params Func<IServiceProvider, ILLMProvider>[] providerFactories
    )
    {
        foreach (var factory in providerFactories)
        {
            services.AddSingleton(factory);
        }

        return services;
    }

    /// <summary>
    /// 使用路由 LLM Provider 替换默认的 ILLMProvider
    /// </summary>
    /// <remarks>
    /// 调用此方法后，注入 ILLMProvider 将获得带路由功能的 Provider
    /// </remarks>
    public static IServiceCollection UseRoutingLLMProvider(this IServiceCollection services)
    {
        // 移除现有的 ILLMProvider 注册
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ILLMProvider));
        if (descriptor != null)
        {
            services.Remove(descriptor);
            // 重新注册为具名服务，供路由器使用
            services.Add(new ServiceDescriptor(
                typeof(ILLMProvider),
                descriptor.ImplementationType ?? descriptor.ImplementationFactory ?? descriptor.ImplementationInstance!,
                descriptor.Lifetime
            ));
        }

        // 注册 RoutingLLMProvider 为主 ILLMProvider
        services.AddSingleton<ILLMProvider>(sp =>
        {
            var router = sp.GetRequiredService<IModelRouter>();
            var options = sp.GetRequiredService<IOptions<ModelRouterOptions>>();
            var logger = sp.GetRequiredService<ILogger<RoutingLLMProvider>>();
            var tokenCounter = sp.GetService<ITokenCounter>();

            return new RoutingLLMProvider(router, options, logger, tokenCounter);
        });

        return services;
    }
}
