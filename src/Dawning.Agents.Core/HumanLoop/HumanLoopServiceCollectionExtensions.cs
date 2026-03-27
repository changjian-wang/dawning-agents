using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// Dependency injection extension methods for human-in-the-loop services.
/// </summary>
public static class HumanLoopServiceCollectionExtensions
{
    /// <summary>
    /// Adds human-in-the-loop services with the auto-approval handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHumanLoop(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<HumanLoopOptions>(configuration, HumanLoopOptions.SectionName);

        services.TryAddSingleton<ApprovalConfig>();
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// Adds human-in-the-loop services with the auto-approval handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHumanLoop(
        this IServiceCollection services,
        Action<HumanLoopOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<HumanLoopOptions>(_ => { });
        }

        services.TryAddSingleton<ApprovalConfig>();
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// Adds the auto-approval handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAutoApprovalHandler(this IServiceCollection services)
    {
        services.TryAddSingleton<IHumanInteractionHandler, AutoApprovalHandler>();
        return services;
    }

    /// <summary>
    /// Adds the async callback handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAsyncCallbackHandler(this IServiceCollection services)
    {
        services.TryAddSingleton<AsyncCallbackHandler>();
        services.TryAddSingleton<IHumanInteractionHandler>(sp =>
            sp.GetRequiredService<AsyncCallbackHandler>()
        );
        return services;
    }

    /// <summary>
    /// Adds the approval workflow.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The approval configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApprovalWorkflow(
        this IServiceCollection services,
        Action<ApprovalConfig>? configure = null
    )
    {
        var config = new ApprovalConfig();
        configure?.Invoke(config);

        services.AddSingleton(config);
        services.TryAddSingleton<ApprovalWorkflow>();

        return services;
    }

    /// <summary>
    /// Adds the human-in-the-loop agent wrapper.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHumanInLoopAgent(
        this IServiceCollection services,
        Action<HumanLoopOptions>? configure = null
    )
    {
        if (configure != null)
        {
            services.AddValidatedOptions(configure);
        }
        else
        {
            services.AddValidatedOptions<HumanLoopOptions>(_ => { });
        }

        services.TryAddScoped<HumanInLoopAgent>();

        return services;
    }
}
