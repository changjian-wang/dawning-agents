namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Agent 系统健康检查
/// </summary>
public sealed class AgentHealthCheck
{
    private readonly List<IHealthCheckProvider> _providers = [];

    /// <summary>
    /// 添加健康检查提供者
    /// </summary>
    public void AddProvider(IHealthCheckProvider provider)
    {
        _providers.Add(provider);
    }

    /// <summary>
    /// 检查系统健康
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
                var health = await provider.CheckHealthAsync(cancellationToken);
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
            Timestamp = DateTime.UtcNow,
            Components = results,
        };
    }

    /// <summary>
    /// 获取所有提供者
    /// </summary>
    public IReadOnlyList<IHealthCheckProvider> Providers => _providers;
}
