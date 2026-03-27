using Dawning.Agents.Abstractions.Resilience;
using Dawning.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// Resilience strategy DI extensions.
/// </summary>
public static class ResilienceServiceCollectionExtensions
{
    /// <summary>
    /// Adds resilience services from <see cref="IConfiguration"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// appsettings.json example:
    /// <code>
    /// {
    ///   "Resilience": {
    ///     "Retry": {
    ///       "MaxRetryAttempts": 3,
    ///       "BaseDelayMs": 1000,
    ///       "UseJitter": true
    ///     },
    ///     "CircuitBreaker": {
    ///       "FailureRatio": 0.5,
    ///       "BreakDurationSeconds": 30
    ///     },
    ///     "Timeout": {
    ///       "TimeoutSeconds": 120
    ///     }
    ///   }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddValidatedOptions<ResilienceOptions>(
            configuration,
            ResilienceOptions.SectionName
        );

        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }

    /// <summary>
    /// Adds resilience services using a configuration delegate.
    /// </summary>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        Action<ResilienceOptions> configure
    )
    {
        services.AddValidatedOptions(configure);
        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }

    /// <summary>
    /// Adds default resilience services.
    /// </summary>
    public static IServiceCollection AddResilience(this IServiceCollection services)
    {
        services.AddValidatedOptions<ResilienceOptions>(_ => { });
        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }
}
