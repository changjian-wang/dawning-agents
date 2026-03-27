using System.Collections.Immutable;

namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Performs health checks for the agent system.
/// </summary>
public sealed class AgentHealthCheck
{
    private ImmutableList<IHealthCheckProvider> _providers =
        ImmutableList<IHealthCheckProvider>.Empty;

    /// <summary>
    /// Adds a health check provider.
    /// </summary>
    public void AddProvider(IHealthCheckProvider provider)
    {
        ImmutableInterlocked.Update(ref _providers, list => list.Add(provider));
    }

    /// <summary>
    /// Checks the overall system health.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<ComponentHealth>();
        var overallHealthy = true;

        foreach (var provider in _providers)
        {
            try
            {
                var health = await provider
                    .CheckHealthAsync(cancellationToken)
                    .ConfigureAwait(false);
                results.Add(health);
                if (health.Status != HealthStatus.Healthy)
                {
                    overallHealthy = false;
                }
            }
            catch (Exception ex)
            {
                results.Add(
                    new ComponentHealth
                    {
                        Name = provider.Name,
                        Status = HealthStatus.Unhealthy,
                        Message = ex.Message,
                    }
                );
                overallHealthy = false;
            }
        }

        return new HealthCheckResult
        {
            Status = overallHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            Timestamp = DateTimeOffset.UtcNow,
            Components = results,
        };
    }

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IReadOnlyList<IHealthCheckProvider> Providers => _providers;
}
