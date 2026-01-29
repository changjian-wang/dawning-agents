using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 延迟优化路由器
/// </summary>
/// <remarks>
/// 选择响应最快的模型。
/// 基于历史统计数据的平均延迟进行选择。
/// </remarks>
public class LatencyOptimizedRouter : ModelRouterBase
{
    public override string Name => "LatencyOptimized";

    public LatencyOptimizedRouter(
        IEnumerable<ILLMProvider> providers,
        IOptions<ModelRouterOptions> options,
        ILogger<LatencyOptimizedRouter>? logger = null
    )
        : base(providers, options, logger ?? NullLogger<LatencyOptimizedRouter>.Instance)
    {
    }

    public override Task<ILLMProvider> SelectProviderAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken = default
    )
    {
        var healthyProviders = GetHealthyProviders(context);

        if (healthyProviders.Count == 0)
        {
            _logger.LogError("没有可用的健康提供者");
            throw new InvalidOperationException("No healthy providers available");
        }

        // 如果有首选模型且可用，优先使用
        if (!string.IsNullOrEmpty(context.PreferredModel))
        {
            var preferred = healthyProviders.FirstOrDefault(
                p => p.Name.Contains(context.PreferredModel, StringComparison.OrdinalIgnoreCase)
            );
            if (preferred != null)
            {
                _logger.LogDebug("使用首选模型: {Provider}", preferred.Name);
                return Task.FromResult(preferred);
            }
        }

        // 按平均延迟排序
        var providerLatencies = healthyProviders
            .Select(p => new
            {
                Provider = p,
                Latency = GetAverageLatency(p.Name)
            })
            .OrderBy(x => x.Latency)
            .ToList();

        // 如果有延迟限制，过滤超出限制的提供者
        if (context.MaxLatencyMs > 0)
        {
            var filtered = providerLatencies
                .Where(x => x.Latency <= context.MaxLatencyMs)
                .ToList();

            if (filtered.Count > 0)
            {
                providerLatencies = filtered;
            }
            else
            {
                _logger.LogWarning(
                    "没有提供者符合延迟限制 {MaxLatencyMs}ms，使用最快的",
                    context.MaxLatencyMs
                );
            }
        }

        var selected = providerLatencies.First().Provider;
        _logger.LogDebug(
            "延迟优化选择: {Provider}，平均延迟: {Latency:F0}ms",
            selected.Name,
            providerLatencies.First().Latency
        );

        return Task.FromResult(selected);
    }

    private double GetAverageLatency(string providerName)
    {
        if (_statistics.TryGetValue(providerName, out var stats) && stats.SuccessfulRequests > 0)
        {
            return stats.AverageLatencyMs;
        }

        // 没有历史数据时使用默认估算
        return GetDefaultLatencyEstimate(providerName);
    }

    private static double GetDefaultLatencyEstimate(string providerName)
    {
        // 基于经验的默认延迟估算（毫秒）
        return providerName.ToLowerInvariant() switch
        {
            var n when n.Contains("ollama") => 200,    // 本地模型最快
            var n when n.Contains("gpt-4o-mini") => 500,
            var n when n.Contains("gpt-4o") => 800,
            var n when n.Contains("gpt-4") => 1500,
            var n when n.Contains("gpt-3.5") => 400,
            var n when n.Contains("claude-3-haiku") => 400,
            var n when n.Contains("claude-3-sonnet") => 800,
            var n when n.Contains("claude-3-opus") => 2000,
            _ => 1000  // 默认估算
        };
    }
}
