using Dawning.Agents.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core;

/// <summary>
/// Options validation service registration extensions.
/// </summary>
public static class OptionsValidationExtensions
{
    /// <summary>
    /// Registers options with startup-time validation (fail-fast).
    /// </summary>
    /// <typeparam name="T">An options type that implements <see cref="IValidatableOptions"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="sectionName">The configuration section name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValidatedOptions<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName
    )
        where T : class, IValidatableOptions
    {
        services.AddOptions<T>().Bind(configuration.GetSection(sectionName)).ValidateOnStart();

        services.TryAddSingleton<IValidateOptions<T>, ValidatableOptionsValidator<T>>();

        return services;
    }

    /// <summary>
    /// Registers options with startup-time validation (using a configuration delegate).
    /// </summary>
    /// <typeparam name="T">An options type that implements <see cref="IValidatableOptions"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddValidatedOptions<T>(
        this IServiceCollection services,
        Action<T> configure
    )
        where T : class, IValidatableOptions
    {
        services.AddOptions<T>().Configure(configure).ValidateOnStart();

        services.TryAddSingleton<IValidateOptions<T>, ValidatableOptionsValidator<T>>();

        return services;
    }
}

/// <summary>
/// Generic options validator — bridges <see cref="IValidatableOptions.Validate"/> to
/// <see cref="IValidateOptions{TOptions}"/>.
/// </summary>
/// <typeparam name="T">The options type.</typeparam>
public class ValidatableOptionsValidator<T> : IValidateOptions<T>
    where T : class, IValidatableOptions
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, T options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail($"{typeof(T).Name} validation failed: {ex.Message}");
        }
    }
}
