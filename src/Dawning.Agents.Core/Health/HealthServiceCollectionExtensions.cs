using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dawning.Agents.Core.Health;

public static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddAgentHealthChecks(this IServiceCollection services)
    {
        var healthChecks = services.AddHealthChecks();
        healthChecks.AddCheck<AgentHealthCheck>("agent");
        healthChecks.AddCheck<LLMProviderHealthCheck>("llm-provider");

        return services;
    }
}
