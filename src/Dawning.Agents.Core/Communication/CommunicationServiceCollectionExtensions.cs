namespace Dawning.Agents.Core.Communication;

using Dawning.Agents.Abstractions.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Communication 相关服务的 DI 扩展
/// </summary>
public static class CommunicationServiceCollectionExtensions
{
    /// <summary>
    /// 添加内存消息总线
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMessageBus(this IServiceCollection services)
    {
        services.TryAddSingleton<IMessageBus, InMemoryMessageBus>();
        return services;
    }

    /// <summary>
    /// 添加内存共享状态
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSharedState(this IServiceCollection services)
    {
        services.TryAddSingleton<ISharedState, InMemorySharedState>();
        return services;
    }

    /// <summary>
    /// 添加完整的通信系统（消息总线 + 共享状态）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCommunication(this IServiceCollection services)
    {
        services.AddMessageBus();
        services.AddSharedState();
        return services;
    }
}
