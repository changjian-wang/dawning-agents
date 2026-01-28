using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Redis.Cache;
using Dawning.Agents.Redis.Lock;
using Dawning.Agents.Redis.Memory;
using Dawning.Agents.Redis.Queue;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Dawning.Agents.Redis;

/// <summary>
/// Redis 服务注册扩展
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Redis 分布式组件（全部）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddRedisDistributed(configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisDistributed(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services.AddRedisCache(configuration);
        services.AddRedisQueue(configuration);
        services.AddRedisLock(configuration);
        services.AddRedisMemory(configuration);

        return services;
    }

    /// <summary>
    /// 添加 Redis 连接
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options =
                configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
                ?? new RedisOptions();

            options.Validate();

            var configOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configOptions.DefaultDatabase = options.DefaultDatabase;
            configOptions.ConnectTimeout = options.ConnectTimeout;
            configOptions.SyncTimeout = options.SyncTimeout;
            configOptions.AsyncTimeout = options.AsyncTimeout;
            configOptions.Ssl = options.UseSsl;
            configOptions.AbortOnConnectFail = options.AbortOnConnectFail;

            return ConnectionMultiplexer.Connect(configOptions);
        });

        return services;
    }

    /// <summary>
    /// 添加 Redis 分布式缓存
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services.TryAddSingleton<IDistributedCache, RedisDistributedCache>();

        return services;
    }

    /// <summary>
    /// 添加 Redis 分布式队列
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedisQueue(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services.Configure<DistributedQueueOptions>(
            configuration.GetSection(DistributedQueueOptions.SectionName)
        );

        services.TryAddSingleton<IDistributedAgentQueue, RedisAgentQueue>();

        return services;
    }

    /// <summary>
    /// 添加 Redis 分布式锁
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedisLock(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services.Configure<DistributedLockOptions>(
            configuration.GetSection(DistributedLockOptions.SectionName)
        );

        services.TryAddSingleton<IDistributedLockFactory, RedisDistributedLockFactory>();

        return services;
    }

    /// <summary>
    /// 添加 Redis 分布式会话记忆
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddRedisMemory(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services.Configure<DistributedSessionOptions>(
            configuration.GetSection(DistributedSessionOptions.SectionName)
        );

        services.TryAddSingleton<RedisMemoryStoreFactory>();

        return services;
    }
}
