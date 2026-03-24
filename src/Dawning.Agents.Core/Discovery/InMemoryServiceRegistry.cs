using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dawning.Agents.Abstractions.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Discovery;

/// <summary>
/// 内存服务注册表 (单进程/开发环境)
/// </summary>
public sealed class InMemoryServiceRegistry : IServiceRegistry, IDisposable
{
    private readonly ConcurrentDictionary<string, ServiceInstance> _instances = new();
    private readonly ConcurrentDictionary<string, Channel<ServiceInstance[]>> _watchers = new();
    private readonly ServiceRegistryOptions _options;
    private readonly ILogger<InMemoryServiceRegistry> _logger;
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed;

    public InMemoryServiceRegistry(
        IOptions<ServiceRegistryOptions>? options = null,
        ILogger<InMemoryServiceRegistry>? logger = null
    )
    {
        _options = options?.Value ?? new ServiceRegistryOptions();
        _logger = logger ?? NullLogger<InMemoryServiceRegistry>.Instance;
        _cleanupTimer = new Timer(
            CleanupExpiredInstances,
            null,
            TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
            TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds)
        );
    }

    public Task RegisterAsync(
        ServiceInstance instance,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(instance);

        _instances[instance.Id] = instance;
        _logger.LogInformation(
            "服务注册: {ServiceName}@{Host}:{Port} (ID={Id})",
            instance.ServiceName,
            instance.Host,
            instance.Port,
            instance.Id
        );
        NotifyWatchers(instance.ServiceName);
        return Task.CompletedTask;
    }

    public Task DeregisterAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        if (_instances.TryRemove(instanceId, out var instance))
        {
            _logger.LogInformation(
                "服务注销: {ServiceName} (ID={Id})",
                instance.ServiceName,
                instanceId
            );
            NotifyWatchers(instance.ServiceName);
        }
        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        if (_instances.TryGetValue(instanceId, out var instance))
        {
            instance.LastHeartbeat = DateTimeOffset.UtcNow;
            instance.IsHealthy = true;
            _logger.LogDebug("心跳: {Id}", instanceId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var instances = _instances
            .Values.Where(i => i.ServiceName == serviceName && i.IsHealthy)
            .ToList();
        return Task.FromResult<IReadOnlyList<ServiceInstance>>(instances);
    }

    public Task<IReadOnlyList<string>> GetServicesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var services = _instances.Values.Select(i => i.ServiceName).Distinct().ToList();
        return Task.FromResult<IReadOnlyList<string>>(services);
    }

    public async IAsyncEnumerable<ServiceInstance[]> WatchAsync(
        string serviceName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var channel = Channel.CreateUnbounded<ServiceInstance[]>();
        var watcherId = $"{serviceName}:{Guid.NewGuid():N}";
        _watchers[watcherId] = channel;

        try
        {
            // 立即返回当前状态
            var current = await GetInstancesAsync(serviceName, cancellationToken)
                .ConfigureAwait(false);
            yield return current.ToArray();

            await foreach (
                var instances in channel
                    .Reader.ReadAllAsync(cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                yield return instances;
            }
        }
        finally
        {
            _watchers.TryRemove(watcherId, out _);
            channel.Writer.TryComplete();
        }
    }

    private void NotifyWatchers(string serviceName)
    {
        var instances = _instances
            .Values.Where(i => i.ServiceName == serviceName && i.IsHealthy)
            .ToArray();

        foreach (var (watcherId, channel) in _watchers)
        {
            if (watcherId.StartsWith(serviceName + ":"))
            {
                channel.Writer.TryWrite(instances);
            }
        }
    }

    private void CleanupExpiredInstances(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var expireThreshold = DateTimeOffset.UtcNow.AddSeconds(-_options.ServiceExpireSeconds);
            var expiredIds = _instances
                .Where(kv => kv.Value.LastHeartbeat < expireThreshold)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                if (_instances.TryRemove(id, out var instance))
                {
                    _logger.LogWarning(
                        "服务过期: {ServiceName} (ID={Id})",
                        instance.ServiceName,
                        id
                    );
                    NotifyWatchers(instance.ServiceName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期实例时发生错误");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cleanupTimer.Dispose();
        foreach (var channel in _watchers.Values)
        {
            channel.Writer.TryComplete();
        }
        _watchers.Clear();
        _instances.Clear();
    }
}
