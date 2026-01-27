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
            // 发送一个简单的测试请求来验证 LLM 可用性
            var testMessages = new[]
            {
                new ChatMessage("user", "ping"),
            };

            var response = await _llmProvider.ChatAsync(
                testMessages,
                new ChatCompletionOptions { MaxTokens = 1 },
                cancellationToken);

            if (!string.IsNullOrEmpty(response.Content))
            {
                _logger.LogDebug("LLMProviderHealthCheck: LLM 正常");
                return HealthCheckResult.Healthy($"LLMProvider ({_llmProvider.Name}) 正常");
            }
            else
            {
                _logger.LogWarning("LLMProviderHealthCheck: LLM 响应为空");
                return HealthCheckResult.Degraded("LLMProvider 响应异常");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "LLMProviderHealthCheck: 检查失败");
            return HealthCheckResult.Unhealthy("LLMProvider 检查失败", ex);
        }
    }
}
