using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// Dependency injection extensions for FluentValidation.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Adds all built-in validators.
    /// </summary>
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        // Register all built-in validators
        services.AddValidatorsFromAssemblyContaining<LLMOptionsValidator>(
            ServiceLifetime.Singleton
        );

        return services;
    }

    /// <summary>
    /// Adds options validation support.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    public static IServiceCollection AddOptionsValidation<TOptions>(
        this IServiceCollection services
    )
        where TOptions : class
    {
        services.TryAddSingleton<IValidateOptions<TOptions>>(sp =>
        {
            var validator = sp.GetService<IValidator<TOptions>>();
            return validator is null
                ? new DataAnnotationValidateOptions<TOptions>(null)
                : new FluentValidationValidateOptions<TOptions>(validator);
        });

        return services;
    }
}

/// <summary>
/// <see cref="IValidateOptions{TOptions}"/> implementation using FluentValidation.
/// </summary>
/// <typeparam name="TOptions">The options type.</typeparam>
internal class FluentValidationValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IValidator<TOptions> _validator;

    public FluentValidationValidateOptions(IValidator<TOptions> validator)
    {
        _validator = validator;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var result = _validator.Validate(options);

        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}");
        return ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>
/// Represents a validation exception.
/// </summary>
public class OptionsValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Errors { get; }

    public OptionsValidationException(IEnumerable<ValidationFailure> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors.ToList();
    }

    public OptionsValidationException(
        IEnumerable<ValidationFailure> errors,
        Exception innerException
    )
        : base(FormatMessage(errors), innerException)
    {
        Errors = errors.ToList();
    }

    private static string FormatMessage(IEnumerable<ValidationFailure> errors)
    {
        var messages = errors.Select(e => $"  - {e.PropertyName}: {e.ErrorMessage}");
        return $"Options validation failed:\n{string.Join("\n", messages)}";
    }
}
