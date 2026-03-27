using Dawning.Agents.Abstractions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Configuration;

/// <summary>
/// DI extensions for configuration hot-reload.
/// </summary>
public static class HotReloadServiceCollectionExtensions
{
    /// <summary>
    /// Adds configuration hot-reload support.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sectionName">The configuration section name.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName
    )
        where TOptions : class
    {
        // Register IOptions<T> and IOptionsMonitor<T>
        services.Configure<TOptions>(configuration.GetSection(sectionName));

        // Register the change notifier
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }

    /// <summary>
    /// Adds configuration hot-reload support with validation.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sectionName">The configuration section name.</param>
    /// <param name="validate">The validation delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, bool> validate
    )
        where TOptions : class
    {
        // Register options with validation
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(validate, $"Validation failed for {typeof(TOptions).Name}");

        // Register the change notifier
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }

    /// <summary>
    /// Adds configuration hot-reload support with validation and a custom failure message.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sectionName">The configuration section name.</param>
    /// <param name="validate">The validation delegate.</param>
    /// <param name="failureMessage">The validation failure message.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, bool> validate,
        string failureMessage
    )
        where TOptions : class
    {
        // Register options with validation
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(validate, failureMessage);

        // Register the change notifier
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }
}
