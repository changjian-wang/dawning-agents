using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Scaling;

/// <summary>
/// 分布式负载均衡策略
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>
    /// 轮询
    /// </summary>
    RoundRobin,

    /// <summary>
    /// 最小连接数
    /// </summary>
    LeastConnections,

    /// <summary>
    /// 一致性哈希（会话粘性）
    /// </summary>
    ConsistentHash,

    /// <summary>
    /// 加权轮询
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// 随机
    /// </summary>
    Random,
}

/// <summary>
/// 分布式负载均衡选项
/// </summary>
public sealed class DistributedLoadBalancerOptions
{
    public const string SectionName = "DistributedLoadBalancer";

    /// <summary>
    /// 默认策略
    /// </summary>
    public LoadBalancingStrategy Strategy { get; set; } = LoadBalancingStrategy.RoundRobin;

    /// <summary>
    /// 虚拟节点数（一致性哈希）
    /// </summary>
    public int VirtualNodeCount { get; set; } = 150;

    /// <summary>
    /// 故障转移重试次数
    /// </summary>
    public int FailoverRetries { get; set; } = 3;

    /// <summary>
    /// 实例健康检查超时（毫秒）
    /// </summary>
    public int HealthCheckTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 启用会话粘性
    /// </summary>
    public bool EnableSessionAffinity { get; set; } = true;
}

/// <summary>
/// 分布式负载均衡器（支持服务发现集成）
/// </summary>
public sealed class DistributedLoadBalancer : IAgentLoadBalancer, IDisposable
{
    private readonly IServiceRegistry? _serviceRegistry;
    private readonly DistributedLoadBalancerOptions _options;
    private readonly ILogger<DistributedLoadBalancer> _logger;

    private readonly List<AgentInstance> _instances = [];
    private readonly SortedDictionary<int, string> _hashRing = [];
    private readonly Dictionary<string, int> _weightedCounters = [];
    private readonly ReaderWriterLockSlim _lock = new();

    private int _roundRobinIndex;
    private CancellationTokenSource? _watchCts;

    public DistributedLoadBalancer(
        IServiceRegistry? serviceRegistry = null,
        IOptions<DistributedLoadBalancerOptions>? options = null,
        ILogger<DistributedLoadBalancer>? logger = null)
    {
        _serviceRegistry = serviceRegistry;
        _options = options?.Value ?? new DistributedLoadBalancerOptions();
        _logger = logger ?? NullLogger<DistributedLoadBalancer>.Instance;
    }

    /// <summary>
    /// 从服务发现同步实例列表
    /// </summary>
    public async Task SyncFromServiceRegistryAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (_serviceRegistry == null)
        {
            _logger.LogWarning("未配置 ServiceRegistry，跳过同步");
            return;
        }

        var instances = await _serviceRegistry.GetInstancesAsync(serviceName, cancellationToken);
        _lock.EnterWriteLock();
        try
        {
            _instances.Clear();
            _hashRing.Clear();

            foreach (var svc in instances)
            {
                var agent = new AgentInstance
                {
                    Id = svc.Id,
                    ServiceName = svc.ServiceName,
                    Endpoint = svc.GetUri().ToString(),
                    IsHealthy = svc.IsHealthy,
                    Weight = svc.Weight,
                };
                _instances.Add(agent);
                AddToHashRing(agent);
            }

            _logger.LogInformation("已从 ServiceRegistry 同步 {Count} 个实例", instances.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 启动 Watch 模式（自动同步实例变更）
    /// </summary>
    public void StartWatching(string serviceName)
    {
        if (_serviceRegistry == null)
        {
            return;
        }

        _watchCts = new CancellationTokenSource();
        _ = WatchLoopAsync(serviceName, _watchCts.Token);
    }

    private async Task WatchLoopAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var instances in _serviceRegistry!.WatchAsync(serviceName, cancellationToken))
            {
                _lock.EnterWriteLock();
                try
                {
                    _instances.Clear();
                    _hashRing.Clear();

                    foreach (var svc in instances)
                    {
                        var agent = new AgentInstance
                        {
                            Id = svc.Id,
                            ServiceName = svc.ServiceName,
                            Endpoint = svc.GetUri().ToString(),
                            IsHealthy = svc.IsHealthy,
                            Weight = svc.Weight,
                        };
                        _instances.Add(agent);
                        AddToHashRing(agent);
                    }

                    _logger.LogDebug("Watch: 更新 {Count} 个实例", instances.Length);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watch 循环异常");
        }
    }

    /// <inheritdoc />
    public void RegisterInstance(AgentInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        _lock.EnterWriteLock();
        try
        {
            var existing = _instances.FindIndex(i => i.Id == instance.Id);
            if (existing >= 0)
            {
                RemoveFromHashRing(_instances[existing]);
                _instances[existing] = instance;
            }
            else
            {
                _instances.Add(instance);
            }

            AddToHashRing(instance);
            _logger.LogInformation("注册实例 {InstanceId}", instance.Id);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void UnregisterInstance(string instanceId)
    {
        _lock.EnterWriteLock();
        try
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                _instances.Remove(instance);
                RemoveFromHashRing(instance);
                _weightedCounters.Remove(instanceId);
                _logger.LogInformation("注销实例 {InstanceId}", instanceId);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public AgentInstance? GetNextInstance() => GetInstance(null);

    /// <summary>
    /// 根据会话 Key 获取实例（支持会话粘性）
    /// </summary>
    public AgentInstance? GetInstance(string? sessionKey)
    {
        _lock.EnterReadLock();
        try
        {
            var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
            if (healthyInstances.Count == 0)
            {
                _logger.LogWarning("没有可用的健康实例");
                return null;
            }

            return _options.Strategy switch
            {
                LoadBalancingStrategy.RoundRobin => GetRoundRobin(healthyInstances),
                LoadBalancingStrategy.LeastConnections => GetLeastConnections(healthyInstances),
                LoadBalancingStrategy.ConsistentHash => GetConsistentHash(sessionKey, healthyInstances),
                LoadBalancingStrategy.WeightedRoundRobin => GetWeightedRoundRobin(healthyInstances),
                LoadBalancingStrategy.Random => GetRandom(healthyInstances),
                _ => GetRoundRobin(healthyInstances),
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public AgentInstance? GetLeastLoadedInstance()
    {
        _lock.EnterReadLock();
        try
        {
            return _instances.Where(i => i.IsHealthy).MinBy(i => i.ActiveRequests);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentInstance> GetAllInstances()
    {
        _lock.EnterReadLock();
        try
        {
            return _instances.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public int HealthyInstanceCount
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _instances.Count(i => i.IsHealthy);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// 故障转移执行
    /// </summary>
    public async Task<T> ExecuteWithFailoverAsync<T>(
        Func<AgentInstance, Task<T>> action,
        string? sessionKey = null,
        CancellationToken cancellationToken = default)
    {
        var triedInstances = new HashSet<string>();

        for (int i = 0; i < _options.FailoverRetries; i++)
        {
            var instance = GetInstanceExcluding(sessionKey, triedInstances);
            if (instance == null)
            {
                throw new InvalidOperationException("没有可用的健康实例");
            }

            triedInstances.Add(instance.Id);

            try
            {
                return await action(instance);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "实例 {InstanceId} 执行失败，尝试故障转移", instance.Id);
                UpdateInstanceHealth(instance.Id, false);
            }
        }

        throw new InvalidOperationException($"故障转移失败，已尝试 {_options.FailoverRetries} 次");
    }

    /// <summary>
    /// 更新实例健康状态
    /// </summary>
    public void UpdateInstanceHealth(string instanceId, bool isHealthy)
    {
        _lock.EnterWriteLock();
        try
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.IsHealthy = isHealthy;
                instance.LastHealthCheck = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 更新实例负载
    /// </summary>
    public void UpdateInstanceLoad(string instanceId, int activeRequests)
    {
        _lock.EnterWriteLock();
        try
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.ActiveRequests = activeRequests;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    #region Private Methods

    private AgentInstance? GetInstanceExcluding(string? sessionKey, HashSet<string> excludeIds)
    {
        _lock.EnterReadLock();
        try
        {
            var available = _instances.Where(i => i.IsHealthy && !excludeIds.Contains(i.Id)).ToList();
            if (available.Count == 0)
            {
                return null;
            }

            return _options.Strategy switch
            {
                LoadBalancingStrategy.ConsistentHash => GetConsistentHash(sessionKey, available),
                _ => GetRoundRobin(available),
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private AgentInstance GetRoundRobin(List<AgentInstance> instances)
    {
        var index = Interlocked.Increment(ref _roundRobinIndex) % instances.Count;
        return instances[index];
    }

    private AgentInstance GetLeastConnections(List<AgentInstance> instances)
    {
        return instances.MinBy(i => i.ActiveRequests) ?? instances[0];
    }

    private AgentInstance GetConsistentHash(string? key, List<AgentInstance> instances)
    {
        if (string.IsNullOrEmpty(key) || _hashRing.Count == 0)
        {
            return GetRoundRobin(instances);
        }

        var hash = GetHash(key);
        var availableIds = instances.Select(i => i.Id).ToHashSet();

        // 找到第一个大于等于 hash 的节点
        foreach (var kv in _hashRing)
        {
            if (kv.Key >= hash && availableIds.Contains(kv.Value))
            {
                return instances.First(i => i.Id == kv.Value);
            }
        }

        // 环绕到第一个可用节点
        foreach (var kv in _hashRing)
        {
            if (availableIds.Contains(kv.Value))
            {
                return instances.First(i => i.Id == kv.Value);
            }
        }

        return GetRoundRobin(instances);
    }

    private AgentInstance GetWeightedRoundRobin(List<AgentInstance> instances)
    {
        // 加权轮询：根据权重分配请求
        var totalWeight = instances.Sum(i => i.Weight);
        if (totalWeight == 0)
        {
            return GetRoundRobin(instances);
        }

        var index = Interlocked.Increment(ref _roundRobinIndex);
        var target = index % totalWeight;
        var cumulative = 0;

        foreach (var instance in instances)
        {
            cumulative += instance.Weight;
            if (target < cumulative)
            {
                return instance;
            }
        }

        return instances[0];
    }

    private AgentInstance GetRandom(List<AgentInstance> instances)
    {
        return instances[Random.Shared.Next(instances.Count)];
    }

    private void AddToHashRing(AgentInstance instance)
    {
        for (int i = 0; i < _options.VirtualNodeCount; i++)
        {
            var virtualKey = $"{instance.Id}#{i}";
            var hash = GetHash(virtualKey);
            _hashRing[hash] = instance.Id;
        }
    }

    private void RemoveFromHashRing(AgentInstance instance)
    {
        for (int i = 0; i < _options.VirtualNodeCount; i++)
        {
            var virtualKey = $"{instance.Id}#{i}";
            var hash = GetHash(virtualKey);
            _hashRing.Remove(hash);
        }
    }

    private static int GetHash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(bytes, 0);
    }

    #endregion

    public void Dispose()
    {
        _watchCts?.Cancel();
        _watchCts?.Dispose();
        _lock.Dispose();
    }
}
