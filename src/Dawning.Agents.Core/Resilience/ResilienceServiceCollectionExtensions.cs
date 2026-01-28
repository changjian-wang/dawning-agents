using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// 弹性策略的依赖注入扩展
/// </summary>
public static class ResilienceServiceCollectionExtensions
{
    /// <summary>
    /// 从 IConfiguration 添加弹性策略服务
    /// </summary>
    /// <remarks>
    /// <para>
    /// appsettings.json 示例:
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
        services.Configure<ResilienceOptions>(
            configuration.GetSection(ResilienceOptions.SectionName)
        );

        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }

    /// <summary>
    /// 添加弹性策略服务（使用配置委托）
    /// </summary>
    public static IServiceCollection AddResilience(
        this IServiceCollection services,
        Action<ResilienceOptions> configure
    )
    {
        services.Configure(configure);
        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }

    /// <summary>
    /// 添加默认弹性策略服务
    /// </summary>
    public static IServiceCollection AddResilience(this IServiceCollection services)
    {
        services.Configure<ResilienceOptions>(_ => { });
        services.TryAddSingleton<IResilienceProvider, PollyResilienceProvider>();

        return services;
    }
}
