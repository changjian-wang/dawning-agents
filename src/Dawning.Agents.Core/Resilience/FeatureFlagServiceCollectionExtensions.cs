using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// DI extensions for feature flags.
/// </summary>
public static class FeatureFlagServiceCollectionExtensions
{
    /// <summary>
    /// Adds the in-memory feature flag implementation.
    /// </summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryFeatureFlag>();
        services.TryAddSingleton<IFeatureFlag>(sp => sp.GetRequiredService<InMemoryFeatureFlag>());
        return services;
    }

    /// <summary>
    /// Adds configuration-backed feature flags (reads from appsettings.json "FeatureFlags" section).
    /// </summary>
    /// <remarks>
    /// Configuration format:
    /// <code>
    /// {
    ///   "FeatureFlags": {
    ///     "NewAgent": { "Enabled": true, "RolloutPercentage": 50 },
    ///     "ExperimentalTool": { "Enabled": false }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddConfigurationFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IFeatureFlag>(sp => new ConfigurationFeatureFlag(
            configuration,
            sp.GetService<ILoggerFactory>()?.CreateLogger<ConfigurationFeatureFlag>()
        ));

        return services;
    }
}
