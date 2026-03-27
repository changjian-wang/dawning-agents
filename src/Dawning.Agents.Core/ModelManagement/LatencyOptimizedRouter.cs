using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Latency-optimized router.
/// </summary>
/// <remarks>
/// Selects the model with the lowest response latency.
/// Based on average latency from historical statistics.
/// </remarks>
public class LatencyOptimizedRouter : ModelRouterBase
{
    public override string Name => "LatencyOptimized";

    public LatencyOptimizedRouter(
        IEnumerable<ILLMProvider> providers,
        IOptions<ModelRouterOptions> options,
        ILogger<LatencyOptimizedRouter>? logger = null
    )
        : base(providers, options, logger ?? NullLogger<LatencyOptimizedRouter>.Instance) { }

    public override Task<ILLMProvider> SelectProviderAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken = default
    )
    {
        var healthyProviders = GetHealthyProviders(context);

        if (healthyProviders.Count == 0)
        {
            _logger.LogError("No healthy providers available");
            throw new InvalidOperationException("No healthy providers available");
        }

        // If a preferred model is specified and available, use it
        if (!string.IsNullOrEmpty(context.PreferredModel))
        {
            var preferred = healthyProviders.FirstOrDefault(p =>
                p.Name.Contains(context.PreferredModel, StringComparison.OrdinalIgnoreCase)
            );
            if (preferred != null)
            {
                _logger.LogDebug("Using preferred model: {Provider}", preferred.Name);
                return Task.FromResult(preferred);
            }
        }

        // Sort by average latency
        var providerLatencies = healthyProviders
            .Select(p => new { Provider = p, Latency = GetAverageLatency(p.Name) })
            .OrderBy(x => x.Latency)
            .ToList();

        // If there is a latency limit, filter providers exceeding it
        if (context.MaxLatencyMs > 0)
        {
            var filtered = providerLatencies.Where(x => x.Latency <= context.MaxLatencyMs).ToList();

            if (filtered.Count > 0)
            {
                providerLatencies = filtered;
            }
            else
            {
                _logger.LogWarning(
                    "No provider meets latency limit {MaxLatencyMs}ms; using the fastest one",
                    context.MaxLatencyMs
                );
            }
        }

        var best = providerLatencies.First();
        var selected = best.Provider;
        _logger.LogDebug(
            "Latency-optimized selection: {Provider}, average latency: {Latency:F0}ms",
            selected.Name,
            best.Latency
        );

        return Task.FromResult(selected);
    }

    private double GetAverageLatency(string providerName)
    {
        if (_statistics.TryGetValue(providerName, out var stats) && stats.SuccessfulRequests > 0)
        {
            return stats.AverageLatencyMs;
        }

        // No historical data; use default estimate
        return GetDefaultLatencyEstimate(providerName);
    }

    private static double GetDefaultLatencyEstimate(string providerName)
    {
        // Experience-based default latency estimates (milliseconds)
        return providerName.ToLowerInvariant() switch
        {
            var n when n.Contains("ollama", StringComparison.Ordinal) => 200, // Local models are fastest
            var n when n.Contains("gpt-4o-mini", StringComparison.Ordinal) => 500,
            var n when n.Contains("gpt-4o", StringComparison.Ordinal) => 800,
            var n when n.Contains("gpt-4", StringComparison.Ordinal) => 1500,
            var n when n.Contains("gpt-3.5", StringComparison.Ordinal) => 400,
            var n when n.Contains("claude-3-haiku", StringComparison.Ordinal) => 400,
            var n when n.Contains("claude-3-sonnet", StringComparison.Ordinal) => 800,
            var n when n.Contains("claude-3-opus", StringComparison.Ordinal) => 2000,
            _ => 1000, // Default estimate
        };
    }
}
