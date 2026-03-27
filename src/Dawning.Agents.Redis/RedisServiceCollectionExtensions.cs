using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Redis.Cache;
using Dawning.Agents.Redis.Communication;
using Dawning.Agents.Redis.Lock;
using Dawning.Agents.Redis.Memory;
using Dawning.Agents.Redis.Queue;
using Dawning.Agents.Redis.Telemetry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis;

/// <summary>
/// Provides extension methods for registering Redis distributed services.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Redis distributed components.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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
        services.AddRedisSharedState();
        services.AddRedisMessageBus();
        services.AddRedisToolUsageTracker();
        services.AddRedisTokenUsageTracker();

        return services;
    }

    /// <summary>
    /// Registers the Redis connection multiplexer.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(RedisOptions)} configuration"
            )
            .ValidateOnStart();

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisOptions>>().Value;

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
    /// Registers the Redis distributed cache.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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
    /// Registers the Redis distributed queue.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisQueue(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services
            .AddOptions<DistributedQueueOptions>()
            .Bind(configuration.GetSection(DistributedQueueOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(DistributedQueueOptions)} configuration"
            )
            .ValidateOnStart();

        services.TryAddSingleton<IDistributedAgentQueue, RedisAgentQueue>();

        return services;
    }

    /// <summary>
    /// Registers the Redis distributed lock.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisLock(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services
            .AddOptions<DistributedLockOptions>()
            .Bind(configuration.GetSection(DistributedLockOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(DistributedLockOptions)} configuration"
            )
            .ValidateOnStart();

        services.TryAddSingleton<IDistributedLockFactory, RedisDistributedLockFactory>();

        return services;
    }

    /// <summary>
    /// Registers the Redis distributed session memory.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisMemory(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRedisConnection(configuration);
        services
            .AddOptions<DistributedSessionOptions>()
            .Bind(configuration.GetSection(DistributedSessionOptions.SectionName))
            .Validate(
                options =>
                {
                    options.Validate();
                    return true;
                },
                $"Invalid {nameof(DistributedSessionOptions)} configuration"
            )
            .ValidateOnStart();

        services.TryAddSingleton<RedisMemoryStoreFactory>();

        return services;
    }

    /// <summary>
    /// Registers the Redis distributed shared state.
    /// </summary>
    public static IServiceCollection AddRedisSharedState(this IServiceCollection services)
    {
        services.RemoveAll<ISharedState>();
        services.AddSingleton<ISharedState, RedisSharedState>();
        return services;
    }

    /// <summary>
    /// Registers the Redis distributed message bus.
    /// </summary>
    public static IServiceCollection AddRedisMessageBus(this IServiceCollection services)
    {
        services.RemoveAll<IMessageBus>();
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        return services;
    }

    /// <summary>
    /// Registers the Redis tool usage tracker.
    /// </summary>
    public static IServiceCollection AddRedisToolUsageTracker(this IServiceCollection services)
    {
        services.RemoveAll<IToolUsageTracker>();
        services.AddSingleton<IToolUsageTracker, RedisToolUsageTracker>();
        return services;
    }

    /// <summary>
    /// Registers the Redis token usage tracker.
    /// </summary>
    public static IServiceCollection AddRedisTokenUsageTracker(this IServiceCollection services)
    {
        services.RemoveAll<ITokenUsageTracker>();
        services.AddSingleton<ITokenUsageTracker, RedisTokenUsageTracker>();
        return services;
    }

    /// <summary>
    /// Registers Redis health checks.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="redisConnectionString">The Redis connection string.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisHealthChecks(
        this IServiceCollection services,
        string redisConnectionString
    )
    {
        var healthChecks = services.AddHealthChecks();
        healthChecks.AddRedis(redisConnectionString, name: "redis");
        services.AddSingleton<IHealthCheck, RedisHealthCheck>();

        return services;
    }
}
