using Dawning.Agents.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core;

/// <summary>
/// 选项验证服务注册扩展
/// </summary>
public static class OptionsValidationExtensions
{
    /// <summary>
    /// 注册选项并启用启动时验证（fail-fast）
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IValidatableOptions"/> 的选项类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="sectionName">配置节名称</param>
    /// <returns>服务集合</returns>
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
    /// 注册选项并启用启动时验证（带配置委托）
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IValidatableOptions"/> 的选项类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
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
/// 通用选项验证器 — 桥接 <see cref="IValidatableOptions.Validate"/> 到
/// <see cref="IValidateOptions{TOptions}"/>
/// </summary>
/// <typeparam name="T">选项类型</typeparam>
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
            return ValidateOptionsResult.Fail($"{typeof(T).Name} 验证失败: {ex.Message}");
        }
    }
}
