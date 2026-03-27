namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent load balancer implementation.
/// </summary>
public sealed class AgentLoadBalancer : IAgentLoadBalancer
{
    private readonly List<AgentInstance> _instances = [];
    private readonly Lock _lock = new();
    private readonly ILogger<AgentLoadBalancer> _logger;
    private int _roundRobinIndex = -1;

    public AgentLoadBalancer(ILogger<AgentLoadBalancer>? logger = null)
    {
        _logger = logger ?? NullLogger<AgentLoadBalancer>.Instance;
    }

    /// <inheritdoc />
    public void RegisterInstance(AgentInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        lock (_lock)
        {
            // Check if already exists
            var existing = _instances.FindIndex(i => i.Id == instance.Id);
            if (existing >= 0)
            {
                _instances[existing] = instance;
                _logger.LogDebug("Updated agent instance {InstanceId}", instance.Id);
            }
            else
            {
                _instances.Add(instance);
                _logger.LogInformation("Registered agent instance {InstanceId}", instance.Id);
            }
        }
    }

    /// <inheritdoc />
    public void UnregisterInstance(string instanceId)
    {
        lock (_lock)
        {
            var removed = _instances.RemoveAll(i => i.Id == instanceId);
            if (removed > 0)
            {
                _logger.LogInformation("Unregistered agent instance {InstanceId}", instanceId);
            }
        }
    }

    /// <inheritdoc />
    public AgentInstance? GetNextInstance()
    {
        lock (_lock)
        {
            var healthyInstances = _instances.Where(i => i.IsHealthy).ToList();
            if (healthyInstances.Count == 0)
            {
                _logger.LogWarning("No healthy instances available");
                return null;
            }

            var index = (int)((uint)(++_roundRobinIndex) % (uint)healthyInstances.Count);
            var instance = healthyInstances[index];
            _logger.LogDebug(
                "Selected instance {InstanceId} (round-robin index: {Index})",
                instance.Id,
                index
            );
            return instance;
        }
    }

    /// <inheritdoc />
    public AgentInstance? GetLeastLoadedInstance()
    {
        lock (_lock)
        {
            var instance = _instances.Where(i => i.IsHealthy).MinBy(i => i.ActiveRequests);

            if (instance != null)
            {
                _logger.LogDebug(
                    "Selected least-loaded instance {InstanceId} (active requests: {ActiveRequests})",
                    instance.Id,
                    instance.ActiveRequests
                );
            }
            else
            {
                _logger.LogWarning("No healthy instances available");
            }

            return instance;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentInstance> GetAllInstances()
    {
        lock (_lock)
        {
            return _instances.ToList();
        }
    }

    /// <inheritdoc />
    public int HealthyInstanceCount
    {
        get
        {
            lock (_lock)
            {
                return _instances.Count(i => i.IsHealthy);
            }
        }
    }

    /// <summary>
    /// Gets the total instance count.
    /// </summary>
    public int TotalInstanceCount
    {
        get
        {
            lock (_lock)
            {
                return _instances.Count;
            }
        }
    }

    /// <summary>
    /// Updates instance health status.
    /// </summary>
    public void UpdateInstanceHealth(string instanceId, bool isHealthy)
    {
        lock (_lock)
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.IsHealthy = isHealthy;
                instance.LastHealthCheck = DateTimeOffset.UtcNow;
                _logger.LogDebug(
                    "Updated instance {InstanceId} health status: {IsHealthy}",
                    instanceId,
                    isHealthy
                );
            }
        }
    }

    /// <summary>
    /// Updates the active request count for an instance.
    /// </summary>
    public void UpdateInstanceLoad(string instanceId, int activeRequests)
    {
        lock (_lock)
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.ActiveRequests = activeRequests;
            }
        }
    }
}
