using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Base class for model routers.
/// </summary>
/// <remarks>
/// Provides common functionality:
/// <list type="bullet">
///   <item>Provider management</item>
///   <item>Statistics collection</item>
///   <item>Health status tracking</item>
/// </list>
/// </remarks>
public abstract class ModelRouterBase : IModelRouter
{
    protected readonly IReadOnlyList<ILLMProvider> _providers;
    protected readonly ConcurrentDictionary<string, ModelStatistics> _statistics = new();
    protected readonly ConcurrentDictionary<string, ProviderHealth> _healthStatus = new();
    protected readonly ModelRouterOptions _options;
    protected readonly ILogger _logger;

    public abstract string Name { get; }

    protected ModelRouterBase(
        IEnumerable<ILLMProvider> providers,
        IOptions<ModelRouterOptions> options,
        ILogger logger
    )
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        _options = options?.Value ?? new ModelRouterOptions();
        _logger = logger ?? NullLogger.Instance;

        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one provider is required", nameof(providers));
        }

        // Initialize statistics and health status
        foreach (var provider in _providers)
        {
            _statistics[provider.Name] = new ModelStatistics { ProviderName = provider.Name };
            _healthStatus[provider.Name] = new ProviderHealth { ProviderName = provider.Name };
        }

        _logger.LogInformation(
            "ModelRouter {Name} initialized with {Count} providers",
            Name,
            _providers.Count
        );
    }

    public abstract Task<ILLMProvider> SelectProviderAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken = default
    );

    public IReadOnlyList<ILLMProvider> GetAvailableProviders()
    {
        return _providers.Where(p => IsProviderHealthy(p.Name)).ToList();
    }

    public void ReportResult(ILLMProvider provider, ModelCallResult result)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(result);

        var name = provider.Name;

        // Update statistics
        if (_statistics.TryGetValue(name, out var stats))
        {
            if (result.Success)
            {
                stats.RecordSuccess(
                    result.InputTokens,
                    result.OutputTokens,
                    result.Cost,
                    result.LatencyMs
                );
            }
            else
            {
                stats.RecordFailure();
            }
        }

        // Update health status
        if (_healthStatus.TryGetValue(name, out var health))
        {
            lock (health)
            {
                if (result.Success)
                {
                    health.ConsecutiveFailures = 0;
                    health.ConsecutiveSuccesses++;

                    if (
                        !health.IsHealthy
                        && health.ConsecutiveSuccesses >= _options.RecoveryThreshold
                    )
                    {
                        health.IsHealthy = true;
                        _logger.LogInformation(
                            "Provider {Provider} has recovered to healthy",
                            name
                        );
                    }
                }
                else
                {
                    health.ConsecutiveSuccesses = 0;
                    health.ConsecutiveFailures++;
                    health.LastError = result.Error;
                    health.LastErrorTime = DateTimeOffset.UtcNow;

                    if (
                        health.IsHealthy
                        && health.ConsecutiveFailures >= _options.UnhealthyThreshold
                    )
                    {
                        health.IsHealthy = false;
                        _logger.LogWarning(
                            "Provider {Provider} marked unhealthy after {Failures} consecutive failures: {Error}",
                            name,
                            health.ConsecutiveFailures,
                            result.Error
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets provider statistics.
    /// </summary>
    public ModelStatistics? GetStatistics(string providerName)
    {
        return _statistics.TryGetValue(providerName, out var stats) ? stats : null;
    }

    /// <summary>
    /// Gets all statistics.
    /// </summary>
    public IReadOnlyDictionary<string, ModelStatistics> GetAllStatistics()
    {
        return new Dictionary<string, ModelStatistics>(_statistics);
    }

    /// <summary>
    /// Checks whether a provider is healthy.
    /// </summary>
    protected bool IsProviderHealthy(string providerName)
    {
        return _healthStatus.TryGetValue(providerName, out var health) && health.IsHealthy;
    }

    /// <summary>
    /// Gets the list of healthy providers.
    /// </summary>
    protected IReadOnlyList<ILLMProvider> GetHealthyProviders(ModelRoutingContext context)
    {
        return _providers
            .Where(p => IsProviderHealthy(p.Name))
            .Where(p => !context.ExcludedProviders.Contains(p.Name))
            .ToList();
    }

    /// <summary>
    /// Provider health status.
    /// </summary>
    protected class ProviderHealth
    {
        public required string ProviderName { get; init; }
        public bool IsHealthy { get; set; } = true;
        public int ConsecutiveFailures { get; set; }
        public int ConsecutiveSuccesses { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset? LastErrorTime { get; set; }
    }
}
