using Dawning.Agents.Abstractions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Configuration;

/// <summary>
/// 配置热重载 DI 扩展方法
/// </summary>
public static class HotReloadServiceCollectionExtensions
{
    /// <summary>
    /// 添加配置热重载支持
    /// </summary>
    /// <typeparam name="TOptions">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="sectionName">配置节名称</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName
    )
        where TOptions : class
    {
        // 注册 IOptions<T> 和 IOptionsMonitor<T>
        services.Configure<TOptions>(configuration.GetSection(sectionName));

        // 注册变更通知器
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }

    /// <summary>
    /// 添加配置热重载支持（带验证）
    /// </summary>
    /// <typeparam name="TOptions">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="sectionName">配置节名称</param>
    /// <param name="validate">验证委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, bool> validate
    )
        where TOptions : class
    {
        // 注册带验证的配置
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(validate, $"Validation failed for {typeof(TOptions).Name}");

        // 注册变更通知器
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }

    /// <summary>
    /// 添加配置热重载支持（带验证和失败消息）
    /// </summary>
    /// <typeparam name="TOptions">配置类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <param name="sectionName">配置节名称</param>
    /// <param name="validate">验证委托</param>
    /// <param name="failureMessage">验证失败消息</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddHotReloadOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Func<TOptions, bool> validate,
        string failureMessage
    )
        where TOptions : class
    {
        // 注册带验证的配置
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(validate, failureMessage);

        // 注册变更通知器
        services.TryAddSingleton<
            IConfigurationChangeNotifier<TOptions>,
            ConfigurationChangeNotifier<TOptions>
        >();

        return services;
    }
}
