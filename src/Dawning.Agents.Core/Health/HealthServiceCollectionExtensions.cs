using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Dawning.Agents.Core.Health;
using StackExchange.Redis;
using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Core.Health;

public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddAgentHealthChecks(
        this IServiceCollection services,
        string? redisConnectionString = null)
    {
        var healthChecks = services.AddHealthChecks();
        healthChecks.AddCheck<AgentHealthCheck>("agent");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecks.AddRedis(redisConnectionString, name: "redis");
            services.AddSingleton<IHealthCheck, RedisHealthCheck>();
        }

        services.AddSingleton<IHealthCheck, AgentHealthCheck>();
        services.AddSingleton<IHealthCheck, LLMProviderHealthCheck>();
        return services;
    }
}
