using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 负载均衡路由器
/// </summary>
/// <remarks>
/// 支持多种负载均衡策略：
/// <list type="bullet">
///   <item>轮询（Round Robin）</item>
///   <item>加权轮询（Weighted Round Robin）</item>
///   <item>随机（Random）</item>
/// </list>
/// </remarks>
public class LoadBalancedRouter : ModelRouterBase
{
    private readonly ModelRoutingStrategy _strategy;
    private int _roundRobinIndex = -1;
    private readonly Random _random = new();
    private readonly Dictionary<string, int> _weights;

    public override string Name => $"LoadBalanced({_strategy})";

    public LoadBalancedRouter(
        IEnumerable<ILLMProvider> providers,
        IOptions<ModelRouterOptions> options,
        ILogger<LoadBalancedRouter>? logger = null
    )
        : base(providers, options, logger ?? NullLogger<LoadBalancedRouter>.Instance)
    {
        _strategy = _options.Strategy;
        _weights = InitializeWeights();
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

        // 如果只有一个提供者，直接返回
        if (healthyProviders.Count == 1)
        {
            return Task.FromResult(healthyProviders[0]);
        }

        ILLMProvider selected = _strategy switch
        {
            ModelRoutingStrategy.RoundRobin => SelectRoundRobin(healthyProviders),
            ModelRoutingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(healthyProviders),
            ModelRoutingStrategy.Random => SelectRandom(healthyProviders),
            _ => SelectRoundRobin(healthyProviders)
        };

        _logger.LogDebug("负载均衡选择: {Provider}", selected.Name);
        return Task.FromResult(selected);
    }

    private ILLMProvider SelectRoundRobin(IReadOnlyList<ILLMProvider> providers)
    {
        var index = Interlocked.Increment(ref _roundRobinIndex);
        return providers[index % providers.Count];
    }

    private ILLMProvider SelectWeightedRoundRobin(IReadOnlyList<ILLMProvider> providers)
    {
        // 计算总权重
        var totalWeight = providers.Sum(p => GetWeight(p.Name));

        // 生成随机数
        var randomValue = _random.Next(totalWeight);

        // 选择提供者
        var currentWeight = 0;
        foreach (var provider in providers)
        {
            currentWeight += GetWeight(provider.Name);
            if (randomValue < currentWeight)
            {
                return provider;
            }
        }

        return providers[^1];
    }

    private ILLMProvider SelectRandom(IReadOnlyList<ILLMProvider> providers)
    {
        var index = _random.Next(providers.Count);
        return providers[index];
    }

    private int GetWeight(string providerName)
    {
        return _weights.TryGetValue(providerName, out var weight) ? weight : 1;
    }

    private Dictionary<string, int> InitializeWeights()
    {
        // 默认权重：本地模型权重高（便宜、快），云模型权重低
        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var name = provider.Name.ToLowerInvariant();
            weights[provider.Name] = name switch
            {
                var n when n.Contains("ollama") => 10,    // 本地优先
                var n when n.Contains("gpt-4o-mini") => 5,
                var n when n.Contains("gpt-3.5") => 4,
                var n when n.Contains("claude-3-haiku") => 4,
                var n when n.Contains("gpt-4o") => 2,
                var n when n.Contains("claude-3-sonnet") => 2,
                var n when n.Contains("gpt-4") => 1,
                var n when n.Contains("claude-3-opus") => 1,
                _ => 1
            };
        }

        return weights;
    }
}
