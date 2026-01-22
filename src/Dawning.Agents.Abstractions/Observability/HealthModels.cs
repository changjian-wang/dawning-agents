namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 健康检查结果
/// </summary>
public record HealthCheckResult
{
    /// <summary>
    /// 整体状态
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// 检查时间
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 各组件健康状态
    /// </summary>
    public IReadOnlyList<ComponentHealth> Components { get; init; } = [];
}

/// <summary>
/// 组件健康状态
/// </summary>
public record ComponentHealth
{
    /// <summary>
    /// 组件名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 健康状态
    /// </summary>
    public HealthStatus Status { get; init; }

    /// <summary>
    /// 消息
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public IDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// 健康状态
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// 健康
    /// </summary>
    Healthy,

    /// <summary>
    /// 降级
    /// </summary>
    Degraded,

    /// <summary>
    /// 不健康
    /// </summary>
    Unhealthy,
}

/// <summary>
/// 健康检查提供者接口
/// </summary>
public interface IHealthCheckProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检查健康状态
    /// </summary>
    Task<ComponentHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}
