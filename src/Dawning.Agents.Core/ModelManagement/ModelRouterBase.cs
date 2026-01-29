using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 模型路由器基类
/// </summary>
/// <remarks>
/// 提供公共功能：
/// <list type="bullet">
///   <item>提供者管理</item>
///   <item>统计信息收集</item>
///   <item>健康状态追踪</item>
/// </list>
/// </remarks>
public abstract class ModelRouterBase : IModelRouter
{
    protected readonly List<ILLMProvider> _providers;
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

        // 初始化统计信息和健康状态
        foreach (var provider in _providers)
        {
            _statistics[provider.Name] = new ModelStatistics { ProviderName = provider.Name };
            _healthStatus[provider.Name] = new ProviderHealth { ProviderName = provider.Name };
        }

        _logger.LogInformation(
            "ModelRouter {Name} 初始化，提供者数量: {Count}",
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
        return _providers
            .Where(p => IsProviderHealthy(p.Name))
            .ToList();
    }

    public void ReportResult(ILLMProvider provider, ModelCallResult result)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(result);

        var name = provider.Name;

        // 更新统计信息
        if (_statistics.TryGetValue(name, out var stats))
        {
            lock (stats)
            {
                stats.TotalRequests++;
                if (result.Success)
                {
                    stats.SuccessfulRequests++;
                    stats.TotalInputTokens += result.InputTokens;
                    stats.TotalOutputTokens += result.OutputTokens;
                    stats.TotalCost += result.Cost;

                    // 更新平均延迟（简单移动平均）
                    stats.AverageLatencyMs = (stats.AverageLatencyMs * (stats.SuccessfulRequests - 1) +
                                              result.LatencyMs) / stats.SuccessfulRequests;
                }
                else
                {
                    stats.FailedRequests++;
                }
                stats.LastUpdated = DateTimeOffset.UtcNow;
            }
        }

        // 更新健康状态
        if (_healthStatus.TryGetValue(name, out var health))
        {
            lock (health)
            {
                if (result.Success)
                {
                    health.ConsecutiveFailures = 0;
                    health.ConsecutiveSuccesses++;

                    if (!health.IsHealthy && health.ConsecutiveSuccesses >= _options.RecoveryThreshold)
                    {
                        health.IsHealthy = true;
                        _logger.LogInformation("提供者 {Provider} 已恢复健康", name);
                    }
                }
                else
                {
                    health.ConsecutiveSuccesses = 0;
                    health.ConsecutiveFailures++;
                    health.LastError = result.Error;
                    health.LastErrorTime = DateTimeOffset.UtcNow;

                    if (health.IsHealthy && health.ConsecutiveFailures >= _options.UnhealthyThreshold)
                    {
                        health.IsHealthy = false;
                        _logger.LogWarning(
                            "提供者 {Provider} 被标记为不健康，连续失败 {Failures} 次: {Error}",
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
    /// 获取提供者统计信息
    /// </summary>
    public ModelStatistics? GetStatistics(string providerName)
    {
        return _statistics.TryGetValue(providerName, out var stats) ? stats : null;
    }

    /// <summary>
    /// 获取所有统计信息
    /// </summary>
    public IReadOnlyDictionary<string, ModelStatistics> GetAllStatistics()
    {
        return _statistics;
    }

    /// <summary>
    /// 检查提供者是否健康
    /// </summary>
    protected bool IsProviderHealthy(string providerName)
    {
        return _healthStatus.TryGetValue(providerName, out var health) && health.IsHealthy;
    }

    /// <summary>
    /// 获取健康的提供者列表
    /// </summary>
    protected IReadOnlyList<ILLMProvider> GetHealthyProviders(ModelRoutingContext context)
    {
        return _providers
            .Where(p => IsProviderHealthy(p.Name))
            .Where(p => !context.ExcludedProviders.Contains(p.Name))
            .ToList();
    }

    /// <summary>
    /// 提供者健康状态
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
