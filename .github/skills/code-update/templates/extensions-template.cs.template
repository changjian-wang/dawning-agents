using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.{Area};

/// <summary>
/// Extension methods for registering {ServiceName}.
/// </summary>
public static class {ServiceName}ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="I{ServiceName}"/> to the service collection.
    /// </summary>
    public static IServiceCollection Add{ServiceName}(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<{ServiceName}Options>(
            configuration.GetSection({ServiceName}Options.SectionName)
        );

        services.TryAddSingleton<I{ServiceName}, {ServiceName}>();

        return services;
    }
}
