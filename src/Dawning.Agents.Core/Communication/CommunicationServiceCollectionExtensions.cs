namespace Dawning.Agents.Core.Communication;

using Dawning.Agents.Abstractions.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI extensions for communication services.
/// </summary>
public static class CommunicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory message bus.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMessageBus(this IServiceCollection services)
    {
        services.TryAddSingleton<IMessageBus, InMemoryMessageBus>();
        return services;
    }

    /// <summary>
    /// Adds the in-memory shared state.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSharedState(this IServiceCollection services)
    {
        services.TryAddSingleton<ISharedState, InMemorySharedState>();
        return services;
    }

    /// <summary>
    /// Adds the full communication system (message bus + shared state).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCommunication(this IServiceCollection services)
    {
        services.AddMessageBus();
        services.AddSharedState();
        return services;
    }
}
