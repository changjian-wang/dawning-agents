namespace Dawning.Agents.Core.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency injection extension methods for orchestration services.
/// </summary>
public static class OrchestrationServiceCollectionExtensions
{
    /// <summary>
    /// Adds orchestration configuration from the specified configuration section.
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<OrchestratorOptions>(
            configuration,
            OrchestratorOptions.SectionName
        );

        return services;
    }

    /// <summary>
    /// Adds orchestration configuration using a delegate.
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        Action<OrchestratorOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        return services;
    }

    /// <summary>
    /// Adds a sequential orchestrator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The orchestrator name.</param>
    /// <param name="configureAgents">A delegate to configure the agent list.</param>
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
    /// Adds a sequential orchestrator with the specified agents.
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
    /// Adds a parallel orchestrator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The orchestrator name.</param>
    /// <param name="configureAgents">A delegate to configure the agent list.</param>
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
    /// Adds a parallel orchestrator with the specified agents.
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
    /// Adds a sequential orchestrator using all registered <see cref="IAgent"/> instances.
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
    /// Adds a parallel orchestrator using all registered <see cref="IAgent"/> instances.
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
