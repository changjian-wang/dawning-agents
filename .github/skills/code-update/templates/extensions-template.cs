using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core;

/// <summary>
/// Extension methods for registering {ServiceName} services.
/// </summary>
public static class {ServiceName}Extensions
{
    /// <summary>
    /// Adds <see cref="I{ServiceName}"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Add{ServiceName}(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<{ServiceName}Options>(
            configuration.GetSection({ServiceName}Options.SectionName));

        // Register service
        services.TryAddSingleton<I{ServiceName}, {ServiceName}>();

        return services;
    }
}
