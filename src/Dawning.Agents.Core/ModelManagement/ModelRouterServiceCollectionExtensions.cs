using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Model router DI extension methods.
/// </summary>
public static class ModelRouterServiceCollectionExtensions
{
    /// <summary>
    /// Adds the model router using configuration.
    /// </summary>
    /// <remarks>
    /// appsettings.json example:
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
        services.AddValidatedOptions<ModelRouterOptions>(
            configuration,
            ModelRouterOptions.SectionName
        );

        return services.AddModelRouterCore();
    }

    /// <summary>
    /// Adds the model router using a configuration delegate.
    /// </summary>
    public static IServiceCollection AddModelRouter(
        this IServiceCollection services,
        Action<ModelRouterOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        return services.AddModelRouterCore();
    }

    /// <summary>
    /// Adds the model router using the specified strategy.
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
    /// Adds the cost-optimized router.
    /// </summary>
    public static IServiceCollection AddCostOptimizedRouter(this IServiceCollection services)
    {
        return services.AddModelRouter(ModelRoutingStrategy.CostOptimized);
    }

    /// <summary>
    /// Adds the latency-optimized router.
    /// </summary>
    public static IServiceCollection AddLatencyOptimizedRouter(this IServiceCollection services)
    {
        return services.AddModelRouter(ModelRoutingStrategy.LatencyOptimized);
    }

    /// <summary>
    /// Adds the load-balanced router.
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
        // Register router factory
        services.TryAddSingleton<IModelRouter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModelRouterOptions>>();
            var providers = sp.GetServices<ILLMProvider>()
                .Where(p => p is not RoutingLLMProvider)
                .ToList();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return options.Value.Strategy switch
            {
                ModelRoutingStrategy.CostOptimized => new CostOptimizedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<CostOptimizedRouter>()
                ),
                ModelRoutingStrategy.LatencyOptimized => new LatencyOptimizedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<LatencyOptimizedRouter>()
                ),
                ModelRoutingStrategy.RoundRobin
                or ModelRoutingStrategy.WeightedRoundRobin
                or ModelRoutingStrategy.Random => new LoadBalancedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<LoadBalancedRouter>()
                ),
                ModelRoutingStrategy.Priority => new CostOptimizedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<CostOptimizedRouter>()
                ),
                _ => new CostOptimizedRouter(
                    providers,
                    options,
                    loggerFactory.CreateLogger<CostOptimizedRouter>()
                ),
            };
        });

        // Register RoutingLLMProvider
        services.TryAddSingleton<RoutingLLMProvider>();

        return services;
    }

    /// <summary>
    /// Adds multiple LLM providers for routing.
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
    /// Replaces the default <see cref="ILLMProvider"/> with a routing-enabled provider.
    /// </summary>
    /// <remarks>
    /// After calling this method, injecting <see cref="ILLMProvider"/> yields a routing-enabled provider.
    /// Internally uses lazy resolution to break the IModelRouter → IEnumerable&lt;ILLMProvider&gt; → RoutingLLMProvider → IModelRouter circular dependency.
    /// </remarks>
    public static IServiceCollection UseRoutingLLMProvider(this IServiceCollection services)
    {
        // Register RoutingLLMProvider as primary ILLMProvider, using Lazy<IModelRouter> to break circular dependency
        services.AddSingleton<ILLMProvider>(sp => new RoutingLLMProvider(
            new Lazy<IModelRouter>(() => sp.GetRequiredService<IModelRouter>()),
            sp.GetRequiredService<IOptions<ModelRouterOptions>>(),
            sp.GetRequiredService<ILogger<RoutingLLMProvider>>(),
            sp.GetService<ITokenCounter>()
        ));

        return services;
    }
}
