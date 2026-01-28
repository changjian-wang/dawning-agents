using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Validation;

/// <summary>
/// FluentValidation 的依赖注入扩展
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// 添加所有内置验证器
    /// </summary>
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        // 注册所有内置验证器
        services.AddValidatorsFromAssemblyContaining<LLMOptionsValidator>(
            ServiceLifetime.Singleton
        );

        return services;
    }

    /// <summary>
    /// 添加 Options 验证支持
    /// </summary>
    /// <typeparam name="TOptions">配置类型</typeparam>
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
/// FluentValidation 的 IValidateOptions 实现
/// </summary>
/// <typeparam name="TOptions">配置类型</typeparam>
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
/// 验证异常
/// </summary>
public class OptionsValidationException : Exception
{
    public IEnumerable<ValidationFailure> Errors { get; }

    public OptionsValidationException(IEnumerable<ValidationFailure> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IEnumerable<ValidationFailure> errors)
    {
        var messages = errors.Select(e => $"  - {e.PropertyName}: {e.ErrorMessage}");
        return $"配置验证失败:\n{string.Join("\n", messages)}";
    }
}
