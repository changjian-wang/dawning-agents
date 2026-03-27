using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.Core.Health;

/// <summary>
/// Agent liveness health check.
/// </summary>
public class AgentHealthCheck : IHealthCheck
{
    private readonly ILogger<AgentHealthCheck> _logger;

    public AgentHealthCheck(ILogger<AgentHealthCheck>? logger = null)
    {
        _logger =
            logger
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentHealthCheck>.Instance;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("AgentHealthCheck: checking agent liveness...");
        // Extensible with custom liveness logic
        return Task.FromResult(HealthCheckResult.Healthy("Agent is running normally"));
    }
}
