namespace Dawning.Agents.Core.Scaling;

using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent 负载均衡器实现
/// </summary>
public class AgentLoadBalancer : IAgentLoadBalancer
{
    private readonly List<AgentInstance> _instances = [];
    private readonly object _lock = new();
    private readonly ILogger<AgentLoadBalancer> _logger;
    private int _roundRobinIndex;

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
            // 检查是否已存在
            var existing = _instances.FindIndex(i => i.Id == instance.Id);
            if (existing >= 0)
            {
                _instances[existing] = instance;
                _logger.LogDebug("已更新 Agent 实例 {InstanceId}", instance.Id);
            }
            else
            {
                _instances.Add(instance);
                _logger.LogInformation("已注册 Agent 实例 {InstanceId}", instance.Id);
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
                _logger.LogInformation("已注销 Agent 实例 {InstanceId}", instanceId);
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
                _logger.LogWarning("没有可用的健康实例");
                return null;
            }

            var index = Interlocked.Increment(ref _roundRobinIndex) % healthyInstances.Count;
            var instance = healthyInstances[index];
            _logger.LogDebug("选择实例 {InstanceId} (轮询索引: {Index})", instance.Id, index);
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
                    "选择负载最小的实例 {InstanceId} (活跃请求: {ActiveRequests})",
                    instance.Id,
                    instance.ActiveRequests
                );
            }
            else
            {
                _logger.LogWarning("没有可用的健康实例");
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
    /// 获取总实例数
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
    /// 更新实例健康状态
    /// </summary>
    public void UpdateInstanceHealth(string instanceId, bool isHealthy)
    {
        lock (_lock)
        {
            var instance = _instances.Find(i => i.Id == instanceId);
            if (instance != null)
            {
                instance.IsHealthy = isHealthy;
                instance.LastHealthCheck = DateTime.UtcNow;
                _logger.LogDebug(
                    "更新实例 {InstanceId} 健康状态: {IsHealthy}",
                    instanceId,
                    isHealthy
                );
            }
        }
    }

    /// <summary>
    /// 更新实例活跃请求数
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
