using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// Token tracking DI extension methods.
/// </summary>
public static class TokenTrackingServiceCollectionExtensions
{
    /// <summary>
    /// Adds token usage tracking services.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="ITokenUsageTracker"/> as a singleton service.
    /// Uses <see cref="InMemoryTokenUsageTracker"/> as the default implementation.
    ///
    /// Usage example:
    /// <code>
    /// services.AddTokenTracking();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddTokenTracking(this IServiceCollection services)
    {
        services.TryAddSingleton<ITokenUsageTracker, InMemoryTokenUsageTracker>();
        return services;
    }

    /// <summary>
    /// Adds token usage tracking services with a custom implementation.
    /// </summary>
    /// <typeparam name="TTracker">The tracker implementation type.</typeparam>
    public static IServiceCollection AddTokenTracking<TTracker>(this IServiceCollection services)
        where TTracker : class, ITokenUsageTracker
    {
        services.TryAddSingleton<ITokenUsageTracker, TTracker>();
        return services;
    }

    /// <summary>
    /// Adds a token tracking decorator for the LLM provider.
    /// </summary>
    /// <remarks>
    /// Automatically wraps the registered <see cref="ILLMProvider"/> to add token tracking.
    /// Requires <see cref="ILLMProvider"/> and <see cref="ITokenUsageTracker"/> to be registered first.
    ///
    /// Usage example:
    /// <code>
    /// services.AddLLMProvider(configuration);
    /// services.AddTokenTracking();
    /// services.AddLLMProviderWithTracking();
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="defaultSource">The default source identifier.</param>
    public static IServiceCollection AddLLMProviderWithTracking(
        this IServiceCollection services,
        string defaultSource = "Default"
    )
    {
        // Ensure token tracking service is registered
        services.AddTokenTracking();

        // Decorate the existing ILLMProvider
        services.Decorate<ILLMProvider>(
            (inner, sp) =>
            {
                var tracker = sp.GetRequiredService<ITokenUsageTracker>();
                return new TokenTrackingLLMProvider(inner, tracker, defaultSource);
            }
        );

        return services;
    }
}

/// <summary>
/// DI decorator extensions.
/// </summary>
internal static class ServiceCollectionDecoratorExtensions
{
    /// <summary>
    /// Decorates an already-registered service.
    /// </summary>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<TService, IServiceProvider, TService> decorator
    )
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));

        if (descriptor == null)
        {
            throw new InvalidOperationException(
                $"Service of type {typeof(TService).Name} is not registered. "
                    + "Please register the service before decorating it."
            );
        }

        // Create a new service descriptor that wraps the original service
        var decoratedDescriptor = ServiceDescriptor.Describe(
            typeof(TService),
            sp =>
            {
                // Create the original service instance
                var inner = CreateInstance<TService>(sp, descriptor);
                // Apply the decorator
                return decorator(inner, sp);
            },
            descriptor.Lifetime
        );

        // Remove the original descriptor and add the new one
        services.Remove(descriptor);
        services.Add(decoratedDescriptor);

        return services;
    }

    private static T CreateInstance<T>(IServiceProvider sp, ServiceDescriptor descriptor)
        where T : class
    {
        if (descriptor.ImplementationInstance != null)
        {
            return (T)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return (T)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType != null)
        {
            return (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Cannot create instance for service descriptor of type {typeof(T).Name}"
        );
    }
}
