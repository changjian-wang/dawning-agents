using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Dawning.Agents.Core.Health;

/// <summary>
/// LLM provider health check.
/// </summary>
public sealed class LLMProviderHealthCheck : IHealthCheck
{
    private readonly ILLMProvider _llmProvider;
    private readonly ILogger<LLMProviderHealthCheck> _logger;

    public LLMProviderHealthCheck(
        ILLMProvider llmProvider,
        ILogger<LLMProviderHealthCheck>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(llmProvider);
        _llmProvider = llmProvider;
        _logger =
            logger
            ?? Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<LLMProviderHealthCheck>
                .Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Send a simple test request to verify LLM availability
            var testMessages = new[] { new ChatMessage("user", "ping") };

            var response = await _llmProvider
                .ChatAsync(
                    testMessages,
                    new ChatCompletionOptions { MaxTokens = 1 },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(response.Content))
            {
                _logger.LogDebug("LLMProviderHealthCheck: LLM is healthy");
                return HealthCheckResult.Healthy($"LLMProvider ({_llmProvider.Name}) is healthy");
            }
            else
            {
                _logger.LogWarning("LLMProviderHealthCheck: LLM response is empty");
                return HealthCheckResult.Degraded("LLMProvider response is abnormal");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "LLMProviderHealthCheck: check failed");
            return HealthCheckResult.Unhealthy("LLMProvider check failed", ex);
        }
    }
}
