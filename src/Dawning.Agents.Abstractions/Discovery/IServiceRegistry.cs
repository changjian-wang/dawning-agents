using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dawning.Agents.Abstractions.Discovery;

/// <summary>
/// 服务实例信息
/// </summary>
public sealed record ServiceInstance
{
    /// <summary>
    /// 服务唯一标识
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 服务地址
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// 服务端口
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// 服务权重 (用于负载均衡)
    /// </summary>
    public int Weight { get; init; } = 100;

    /// <summary>
    /// 服务标签
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// 健康检查端点
    /// </summary>
    public string? HealthCheckUrl { get; init; }

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后心跳时间
    /// </summary>
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否健康
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// 获取服务 URI
    /// </summary>
    public Uri GetUri(string scheme = "http") => new($"{scheme}://{Host}:{Port}");
}

/// <summary>
/// 服务注册选项
/// </summary>
public sealed class ServiceRegistryOptions
{
    public const string SectionName = "ServiceRegistry";

    /// <summary>
    /// 心跳间隔 (秒)
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 服务过期时间 (秒)
    /// </summary>
    public int ServiceExpireSeconds { get; set; } = 30;

    /// <summary>
    /// 健康检查间隔 (秒)
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 15;

    public void Validate()
    {
        if (HeartbeatIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("HeartbeatIntervalSeconds must be positive");
        }

        if (ServiceExpireSeconds <= HeartbeatIntervalSeconds)
        {
            throw new InvalidOperationException("ServiceExpireSeconds must be greater than HeartbeatIntervalSeconds");
        }
    }
}

/// <summary>
/// 服务注册接口
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// 注册服务实例
    /// </summary>
    Task RegisterAsync(ServiceInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 注销服务实例
    /// </summary>
    Task DeregisterAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送心跳
    /// </summary>
    Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定服务的所有健康实例
    /// </summary>
    Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已注册的服务名称
    /// </summary>
    Task<IReadOnlyList<string>> GetServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 监听服务变更
    /// </summary>
    IAsyncEnumerable<ServiceInstance[]> WatchAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}
