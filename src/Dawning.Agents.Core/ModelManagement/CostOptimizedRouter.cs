using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 成本优化路由器
/// </summary>
/// <remarks>
/// 选择预估成本最低的模型。
/// 考虑因素：
/// <list type="bullet">
///   <item>模型定价（输入/输出 token 价格）</item>
///   <item>预估 token 数量</item>
///   <item>提供者健康状态</item>
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

        // 计算每个提供者的预估成本
        var providerCosts = healthyProviders
            .Select(p => new
            {
                Provider = p,
                Cost = CalculateEstimatedCost(p.Name, context)
            })
            .OrderBy(x => x.Cost)
            .ToList();

        // 如果有成本限制，过滤超出限制的提供者
        if (context.MaxCost > 0)
        {
            providerCosts = providerCosts
                .Where(x => x.Cost <= context.MaxCost)
                .ToList();

            if (providerCosts.Count == 0)
            {
                _logger.LogWarning(
                    "没有提供者符合成本限制 {MaxCost}，使用最便宜的",
                    context.MaxCost
                );
                providerCosts = healthyProviders
                    .Select(p => new
                    {
                        Provider = p,
                        Cost = CalculateEstimatedCost(p.Name, context)
                    })
                    .OrderBy(x => x.Cost)
                    .ToList();
            }
        }

        var selected = providerCosts.First().Provider;
        _logger.LogDebug(
            "成本优化选择: {Provider}，预估成本: ${Cost:F6}",
            selected.Name,
            providerCosts.First().Cost
        );

        return Task.FromResult(selected);
    }

    private decimal CalculateEstimatedCost(string providerName, ModelRoutingContext context)
    {
        if (!_pricingMap.TryGetValue(providerName, out var pricing))
        {
            // 未知提供者使用默认定价
            pricing = ModelPricing.KnownPricing.GetPricing(providerName);
        }

        return pricing.CalculateCost(context.EstimatedInputTokens, context.EstimatedOutputTokens);
    }

    private Dictionary<string, ModelPricing> BuildPricingMap()
    {
        var map = new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase);

        // 添加自定义定价配置
        foreach (var (name, pricing) in _options.CustomPricing)
        {
            map[name] = pricing;
        }

        // 为每个提供者设置定价
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
