using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Handoff;

/// <summary>
/// Dependency injection extension methods for handoff services.
/// </summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Adds the handoff handler.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHandoff(
        this IServiceCollection services,
        IConfiguration? configuration = null
    )
    {
        if (configuration != null)
        {
            services.AddValidatedOptions<HandoffOptions>(configuration, HandoffOptions.SectionName);
        }

        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        return services;
    }

    /// <summary>
    /// Adds the handoff handler with options configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">The configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHandoff(
        this IServiceCollection services,
        Action<HandoffOptions> configureOptions
    )
    {
        services.AddValidatedOptions(configureOptions);
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        return services;
    }

    /// <summary>
    /// Registers an agent with the handoff system.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="agent">The agent to register.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAgentToHandoff(
        this IServiceCollection services,
        IAgent agent
    )
    {
        // Ensure handoff services are registered
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        // Add a post-processor to register the agent
        services.AddSingleton<IHandoffAgentRegistration>(new HandoffAgentRegistration(agent));

        return services;
    }

    /// <summary>
    /// Registers an agent with the handoff system using a factory method.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="agentFactory">The agent factory method.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAgentToHandoff(
        this IServiceCollection services,
        Func<IServiceProvider, IAgent> agentFactory
    )
    {
        // Ensure handoff services are registered
        services.TryAddSingleton<IHandoffHandler, HandoffHandler>();

        // Add a post-processor to register the agent
        services.AddSingleton<IHandoffAgentRegistration>(sp => new HandoffAgentRegistration(
            agentFactory(sp)
        ));

        return services;
    }

    /// <summary>
    /// Ensures all registered agents are registered with the handoff system.
    /// </summary>
    /// <param name="services">The service provider.</param>
    public static void EnsureHandoffAgentsRegistered(this IServiceProvider services)
    {
        var handler = services.GetService<IHandoffHandler>();
        if (handler == null)
        {
            return;
        }

        var registrations = services.GetServices<IHandoffAgentRegistration>();
        foreach (var registration in registrations)
        {
            handler.RegisterAgent(registration.Agent);
        }
    }
}

/// <summary>
/// Marker interface for handoff agent registration.
/// </summary>
public interface IHandoffAgentRegistration
{
    /// <summary>
    /// Gets the agent to register.
    /// </summary>
    IAgent Agent { get; }
}

/// <summary>
/// Handoff agent registration implementation.
/// </summary>
internal class HandoffAgentRegistration : IHandoffAgentRegistration
{
    public HandoffAgentRegistration(IAgent agent)
    {
        Agent = agent;
    }

    public IAgent Agent { get; }
}
