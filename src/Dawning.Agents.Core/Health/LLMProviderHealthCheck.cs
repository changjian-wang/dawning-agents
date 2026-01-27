using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Dawning.Agents.Abstractions.LLM;
using System.Threading;
using System.Threading.Tasks;

namespace Dawning.Agents.Core.Health;

/// <summary>
/// LLMProvider 健康检查
/// </summary>
public class LLMProviderHealthCheck : IHealthCheck
{
    private readonly ILLMProvider _llmProvider;
    private readonly ILogger<LLMProviderHealthCheck> _logger;

    public LLMProviderHealthCheck(
        ILLMProvider llmProvider,
        ILogger<LLMProviderHealthCheck>? logger = null)
    {
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LLMProviderHealthCheck>.Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _llmProvider.PingAsync(cancellationToken);
            if (result)
            {
                _logger.LogDebug("LLMProviderHealthCheck: LLM 正常");
                return HealthCheckResult.Healthy("LLMProvider 正常");
            }
            else
            {
                _logger.LogWarning("LLMProviderHealthCheck: LLM 不可用");
                return HealthCheckResult.Unhealthy("LLMProvider 不可用");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "LLMProviderHealthCheck: 检查失败");
            return HealthCheckResult.Unhealthy("LLMProvider 检查失败", ex);
        }
    }
}
