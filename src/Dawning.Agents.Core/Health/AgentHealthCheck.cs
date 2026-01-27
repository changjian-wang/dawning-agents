using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Dawning.Agents.Core.Health;

/// <summary>
/// Agent 存活健康检查
/// </summary>
public class AgentHealthCheck : IHealthCheck
{
    private readonly ILogger<AgentHealthCheck> _logger;

    public AgentHealthCheck(ILogger<AgentHealthCheck>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentHealthCheck>.Instance;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AgentHealthCheck: 正在检查 Agent 存活状态...");
        // 可扩展为自定义存活逻辑
        return Task.FromResult(HealthCheckResult.Healthy("Agent 正常运行"));
    }
}
