using System.Security.Cryptography;
using System.Text;
using Dawning.Agents.Abstractions.Discovery;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Scaling;

/// <summary>
/// Distributed load balancing strategy.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>
    /// Round robin.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Least connections.
    /// </summary>
    LeastConnections,

    /// <summary>
    /// Consistent hash (session affinity).
    /// </summary>
    ConsistentHash,

    /// <summary>
    /// Weighted round robin.
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// Random.
    /// </summary>
    Random,
}

/// <summary>
/// Distributed load balancer options.
/// </summary>
public sealed class DistributedLoadBalancerOptions
{
    public const string SectionName = "DistributedLoadBalancer";

    /// <summary>
    /// Gets or sets the default strategy.
    /// </summary>
    public LoadBalancingStrategy Strategy { get; set; } = LoadBalancingStrategy.RoundRobin;

    /// <summary>
    /// Gets or sets the virtual node count for consistent hashing.
    /// </summary>
    public int VirtualNodeCount { get; set; } = 150;

    /// <summary>
    /// Gets or sets the number of failover retries.
    /// </summary>
    public int FailoverRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the instance health check timeout in milliseconds.
    /// </summary>
    public int HealthCheckTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets a value indicating whether session affinity is enabled.
    /// </summary>
    public bool EnableSessionAffinity { get; set; } = true;
}

/// <summary>
/// Distributed load balancer with service discovery integration.
/// </summary>
public sealed class DistributedLoadBalancer : IAgentLoadBalancer, IDisposable, IAsyncDisposable
{
    private readonly IServiceRegistry? _serviceRegistry;
    private readonly DistributedLoadBalancerOptions _options;
    private readonly ILogger<DistributedLoadBalancer> _logger;

    private readonly List<AgentInstance> _instances = [];
    private readonly SortedDictionary<int, string> _hashRing = [];
    private readonly ReaderWriterLockSlim _lock = new();

    private int _roundRobinIndex;
    private CancellationTokenSource? _watchCts;
    private Task? _watchTask;
    private volatile bool _disposed;

    public DistributedLoadBalancer(
        IServiceRegistry? serviceRegistry = null,
        IOptions<DistributedLoadBalancerOptions>? options = null,
        ILogger<DistributedLoadBalancer>? logger = null
    )
    {
        _serviceRegistry = serviceRegistry;
        _options = options?.Value ?? new DistributedLoadBalancerOptions();
        _logger = logger ?? NullLogger<DistributedLoadBalancer>.Instance;

        ArgumentOutOfRangeException.ThrowIfNegative(_options.FailoverRetries);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.VirtualNodeCount);
    }

    /// <summary>
    /// Synchronizes the instance list from the service registry.
    /// </summary>
    public async Task SyncFromServiceRegistryAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_serviceRegistry == null)
        {
            _logger.LogWarning("ServiceRegistry is not configured; skipping synchronization");
            return;
        }

        var instances = await _serviceRegistry
            .GetInstancesAsync(serviceName, cancellationToken)
            .ConfigureAwait(false);
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

            _logger.LogInformation("Synchronized {Count} instances from ServiceRegistry", instances.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Starts watch mode for automatic instance synchronization.
    /// </summary>
    public void StartWatching(string serviceName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_serviceRegistry == null)
        {
            return;
        }

        // Cancel and dispose the previous watch (use Interlocked.Exchange to prevent race conditions)
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _watchCts, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var task = WatchLoopAsync(serviceName, newCts.Token);
        _watchTask = task;
        _ = task.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Unobserved exception occurred in watch loop");
                }
            },
            TaskScheduler.Default
        );
    }

    private async Task WatchLoopAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                var instances in _serviceRegistry!
                    .WatchAsync(serviceName, cancellationToken)
                    .ConfigureAwait(false)
            )
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

                    _logger.LogDebug("Watch: updated {Count} instances", instances.Length);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watch loop exception");
        }
    }

    /// <inheritdoc />
    public void RegisterInstance(AgentInstance instance)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            _logger.LogInformation("Registered instance {InstanceId}", instance.Id);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void UnregisterInstance(string instanceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lock.EnterWriteLock();
        try
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                _instances.Remove(instance);
                RemoveFromHashRing(instance);
                _logger.LogInformation("Unregistered instance {InstanceId}", instanceId);
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
    /// Gets an instance by session key with session affinity support.
    /// </summary>
    public AgentInstance? GetInstance(string? sessionKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lock.EnterReadLock();
        try
        {
            var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
            if (healthyInstances.Count == 0)
            {
                _logger.LogWarning("No healthy instances available");
                return null;
            }

            return _options.Strategy switch
            {
                LoadBalancingStrategy.RoundRobin => GetRoundRobin(healthyInstances),
                LoadBalancingStrategy.LeastConnections => GetLeastConnections(healthyInstances),
                LoadBalancingStrategy.ConsistentHash => GetConsistentHash(
                    sessionKey,
                    healthyInstances
                ),
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            ObjectDisposedException.ThrowIf(_disposed, this);
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
    /// Executes an action with failover.
    /// </summary>
    public async Task<T> ExecuteWithFailoverAsync<T>(
        Func<AgentInstance, Task<T>> action,
        string? sessionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var triedInstances = new HashSet<string>();

        for (int i = 0; i <= _options.FailoverRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var instance = GetInstanceExcluding(sessionKey, triedInstances);
            if (instance == null)
            {
                throw new InvalidOperationException("No healthy instances available");
            }

            triedInstances.Add(instance.Id);

            try
            {
                return await action(instance).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Instance {InstanceId} execution failed; attempting failover", instance.Id);
                UpdateInstanceHealth(instance.Id, false);
            }
        }

        throw new InvalidOperationException(
            $"Failover failed after {_options.FailoverRetries + 1} attempts"
        );
    }

    /// <summary>
    /// Updates instance health status.
    /// </summary>
    public void UpdateInstanceHealth(string instanceId, bool isHealthy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lock.EnterWriteLock();
        try
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.IsHealthy = isHealthy;
                instance.LastHealthCheck = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Updates instance load.
    /// </summary>
    public void UpdateInstanceLoad(string instanceId, int activeRequests)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            var available = _instances
                .Where(i => i.IsHealthy && !excludeIds.Contains(i.Id))
                .ToList();
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
        var index = (int)(
            (uint)Interlocked.Increment(ref _roundRobinIndex) % (uint)instances.Count
        );
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

        // Find the first node with a hash >= the key hash
        foreach (var kv in _hashRing)
        {
            if (kv.Key >= hash && availableIds.Contains(kv.Value))
            {
                return instances.First(i => i.Id == kv.Value);
            }
        }

        // Wrap around to the first available node
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
        // Weighted round robin: distribute requests by weight
        var totalWeight = instances.Sum(i => i.Weight);
        if (totalWeight == 0)
        {
            return GetRoundRobin(instances);
        }

        var index = Interlocked.Increment(ref _roundRobinIndex);
        var target = (int)((uint)index % (uint)totalWeight);
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var cts = Interlocked.Exchange(ref _watchCts, null);
        cts?.Cancel();

        // Best-effort wait for the watch task to exit before disposing the lock
        try
        {
            _watchTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Timeout or AggregateException — safe to ignore during disposal
        }

        cts?.Dispose();
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var cts = Interlocked.Exchange(ref _watchCts, null);
        if (cts != null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (_watchTask != null)
        {
            try
            {
                await _watchTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Watch loop already handles its own exceptions
            }
        }

        cts?.Dispose();
        _lock.Dispose();
    }
}
