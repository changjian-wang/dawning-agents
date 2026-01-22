using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// Token 追踪相关的 DI 扩展方法
/// </summary>
public static class TokenTrackingServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Token 使用追踪服务
    /// </summary>
    /// <remarks>
    /// 注册 ITokenUsageTracker 单例服务。
    /// 使用 InMemoryTokenUsageTracker 作为默认实现。
    ///
    /// 使用示例:
    /// <code>
    /// services.AddTokenTracking();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddTokenTracking(this IServiceCollection services)
    {
        services.TryAddSingleton<ITokenUsageTracker, InMemoryTokenUsageTracker>();
        return services;
    }

    /// <summary>
    /// 添加 Token 使用追踪服务（使用自定义实现）
    /// </summary>
    /// <typeparam name="TTracker">追踪器实现类型</typeparam>
    public static IServiceCollection AddTokenTracking<TTracker>(this IServiceCollection services)
        where TTracker : class, ITokenUsageTracker
    {
        services.TryAddSingleton<ITokenUsageTracker, TTracker>();
        return services;
    }

    /// <summary>
    /// 为 LLM Provider 添加 Token 追踪装饰器
    /// </summary>
    /// <remarks>
    /// 自动包装已注册的 ILLMProvider，添加 Token 追踪功能。
    /// 必须先注册 ILLMProvider 和 ITokenUsageTracker。
    ///
    /// 使用示例:
    /// <code>
    /// services.AddLLMProvider(configuration);
    /// services.AddTokenTracking();
    /// services.AddLLMProviderWithTracking();
    /// </code>
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="defaultSource">默认来源标识</param>
    public static IServiceCollection AddLLMProviderWithTracking(
        this IServiceCollection services,
        string defaultSource = "Default"
    )
    {
        // 确保 Token 追踪服务已注册
        services.AddTokenTracking();

        // 装饰现有的 ILLMProvider
        services.Decorate<ILLMProvider>(
            (inner, sp) =>
            {
                var tracker = sp.GetRequiredService<ITokenUsageTracker>();
                return new TokenTrackingLLMProvider(inner, tracker, defaultSource);
            }
        );

        return services;
    }
}

/// <summary>
/// DI 装饰器扩展
/// </summary>
internal static class ServiceCollectionDecoratorExtensions
{
    /// <summary>
    /// 装饰已注册的服务
    /// </summary>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<TService, IServiceProvider, TService> decorator
    )
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));

        if (descriptor == null)
        {
            throw new InvalidOperationException(
                $"Service of type {typeof(TService).Name} is not registered. "
                    + "Please register the service before decorating it."
            );
        }

        // 创建一个新的服务描述符，包装原有的服务
        var decoratedDescriptor = ServiceDescriptor.Describe(
            typeof(TService),
            sp =>
            {
                // 创建原始服务实例
                var inner = CreateInstance<TService>(sp, descriptor);
                // 应用装饰器
                return decorator(inner, sp);
            },
            descriptor.Lifetime
        );

        // 移除原有的描述符并添加新的
        services.Remove(descriptor);
        services.Add(decoratedDescriptor);

        return services;
    }

    private static T CreateInstance<T>(IServiceProvider sp, ServiceDescriptor descriptor)
        where T : class
    {
        if (descriptor.ImplementationInstance != null)
        {
            return (T)descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return (T)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType != null)
        {
            return (T)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Cannot create instance for service descriptor of type {typeof(T).Name}"
        );
    }
}
