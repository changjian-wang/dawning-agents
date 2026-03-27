using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Load-balanced router.
/// </summary>
/// <remarks>
/// Supports multiple load balancing strategies:
/// <list type="bullet">
///   <item>Round Robin</item>
///   <item>Weighted Round Robin</item>
///   <item>Random</item>
/// </list>
/// </remarks>
public class LoadBalancedRouter : ModelRouterBase
{
    private readonly ModelRoutingStrategy _strategy;
    private int _roundRobinIndex = -1;
    private readonly Lock _randomLock = new();
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
            _logger.LogError("No healthy providers available");
            throw new InvalidOperationException("No healthy providers available");
        }

        // If only one provider, return it directly
        if (healthyProviders.Count == 1)
        {
            return Task.FromResult(healthyProviders[0]);
        }

        ILLMProvider selected = _strategy switch
        {
            ModelRoutingStrategy.RoundRobin => SelectRoundRobin(healthyProviders),
            ModelRoutingStrategy.WeightedRoundRobin => SelectWeightedRoundRobin(healthyProviders),
            ModelRoutingStrategy.Random => SelectRandom(healthyProviders),
            _ => SelectRoundRobin(healthyProviders),
        };

        _logger.LogDebug("Load-balanced selection: {Provider}", selected.Name);
        return Task.FromResult(selected);
    }

    private ILLMProvider SelectRoundRobin(IReadOnlyList<ILLMProvider> providers)
    {
        var index = Interlocked.Increment(ref _roundRobinIndex);
        return providers[(int)((uint)index % (uint)providers.Count)];
    }

    private ILLMProvider SelectWeightedRoundRobin(IReadOnlyList<ILLMProvider> providers)
    {
        // Calculate total weight
        var totalWeight = providers.Sum(p => GetWeight(p.Name));

        // Generate random number
        int randomValue;
        lock (_randomLock)
        {
            randomValue = _random.Next(totalWeight);
        }

        // Select provider
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
        int index;
        lock (_randomLock)
        {
            index = _random.Next(providers.Count);
        }
        return providers[index];
    }

    private int GetWeight(string providerName)
    {
        return _weights.TryGetValue(providerName, out var weight) ? weight : 1;
    }

    private Dictionary<string, int> InitializeWeights()
    {
        // Default weights: local models have higher weight (cheaper, faster); cloud models have lower weight
        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var name = provider.Name.ToLowerInvariant();
            weights[provider.Name] = name switch
            {
                var n when n.Contains("ollama", StringComparison.Ordinal) => 10, // Prefer local
                var n when n.Contains("gpt-4o-mini", StringComparison.Ordinal) => 5,
                var n when n.Contains("gpt-3.5", StringComparison.Ordinal) => 4,
                var n when n.Contains("claude-3-haiku", StringComparison.Ordinal) => 4,
                var n when n.Contains("gpt-4o", StringComparison.Ordinal) => 2,
                var n when n.Contains("claude-3-sonnet", StringComparison.Ordinal) => 2,
                var n when n.Contains("gpt-4", StringComparison.Ordinal) => 1,
                var n when n.Contains("claude-3-opus", StringComparison.Ordinal) => 1,
                _ => 1,
            };
        }

        return weights;
    }
}
