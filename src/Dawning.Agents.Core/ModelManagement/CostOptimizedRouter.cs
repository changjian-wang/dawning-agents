using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Cost-optimized router.
/// </summary>
/// <remarks>
/// Selects the model with the lowest estimated cost.
/// Factors considered:
/// <list type="bullet">
///   <item>Model pricing (input/output token prices)</item>
///   <item>Estimated token counts</item>
///   <item>Provider health status</item>
/// </list>
/// </remarks>
public class CostOptimizedRouter : ModelRouterBase
{
    private readonly Dictionary<string, ModelPricing> _pricingMap;

    public override string Name => "CostOptimized";

    public CostOptimizedRouter(
        IEnumerable<ILLMProvider> providers,
        IOptions<ModelRouterOptions> options,
        ILogger<CostOptimizedRouter>? logger = null
    )
        : base(providers, options, logger ?? NullLogger<CostOptimizedRouter>.Instance)
    {
        _pricingMap = BuildPricingMap();
    }

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
            // Prefer exact match, then partial match
            var preferred =
                healthyProviders.FirstOrDefault(p =>
                    p.Name.Equals(context.PreferredModel, StringComparison.OrdinalIgnoreCase)
                )
                ?? healthyProviders.FirstOrDefault(p =>
                    p.Name.Contains(context.PreferredModel, StringComparison.OrdinalIgnoreCase)
                );
            if (preferred != null)
            {
                _logger.LogDebug("Using preferred model: {Provider}", preferred.Name);
                return Task.FromResult(preferred);
            }
        }

        // Calculate estimated cost for each provider
        var providerCosts = healthyProviders
            .Select(p => new { Provider = p, Cost = CalculateEstimatedCost(p.Name, context) })
            .OrderBy(x => x.Cost)
            .ToList();

        // If there is a cost limit, filter providers exceeding it
        if (context.MaxCost > 0)
        {
            providerCosts = providerCosts.Where(x => x.Cost <= context.MaxCost).ToList();

            if (providerCosts.Count == 0)
            {
                _logger.LogWarning(
                    "No provider meets cost limit {MaxCost}; using the cheapest one",
                    context.MaxCost
                );
                providerCosts = healthyProviders
                    .Select(p => new
                    {
                        Provider = p,
                        Cost = CalculateEstimatedCost(p.Name, context),
                    })
                    .OrderBy(x => x.Cost)
                    .ToList();
            }
        }

        var best = providerCosts.First();
        var selected = best.Provider;
        _logger.LogDebug(
            "Cost-optimized selection: {Provider}, estimated cost: ${Cost:F6}",
            selected.Name,
            best.Cost
        );

        return Task.FromResult(selected);
    }

    private decimal CalculateEstimatedCost(string providerName, ModelRoutingContext context)
    {
        if (!_pricingMap.TryGetValue(providerName, out var pricing))
        {
            // Unknown provider uses default pricing
            pricing = ModelPricing.KnownPricing.GetPricing(providerName);
        }

        return pricing.CalculateCost(context.EstimatedInputTokens, context.EstimatedOutputTokens);
    }

    private Dictionary<string, ModelPricing> BuildPricingMap()
    {
        var map = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase);

        // Add custom pricing configuration
        foreach (var (name, pricing) in _options.CustomPricing)
        {
            map[name] = pricing;
        }

        // Set pricing for each provider
        foreach (var provider in _providers)
        {
            if (!map.ContainsKey(provider.Name))
            {
                map[provider.Name] = ModelPricing.KnownPricing.GetPricing(provider.Name);
            }
        }

        return map;
    }
}
