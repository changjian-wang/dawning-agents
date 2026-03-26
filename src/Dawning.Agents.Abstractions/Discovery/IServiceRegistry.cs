using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Discovery;

/// <summary>
/// Represents a service instance.
/// </summary>
public sealed record ServiceInstance
{
    /// <summary>
    /// The unique identifier of the service instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name of the service.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// The host address of the service.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The port number of the service.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// The weight of the service instance, used for load balancing.
    /// </summary>
    public int Weight { get; init; } = 100;

    /// <summary>
    /// The metadata tags associated with the service instance.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The URL for the health check endpoint.
    /// </summary>
    public string? HealthCheckUrl { get; init; }

    /// <summary>
    /// The time when the service instance was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    private long _lastHeartbeatTicks = DateTimeOffset.UtcNow.UtcTicks;

    /// <summary>
    /// The time of the last heartbeat.
    /// </summary>
    public DateTimeOffset LastHeartbeat
    {
        get => new(Volatile.Read(ref _lastHeartbeatTicks), TimeSpan.Zero);
        set => Volatile.Write(ref _lastHeartbeatTicks, value.UtcTicks);
    }

    private bool _isHealthy = true;

    /// <summary>
    /// Gets or sets a value indicating whether the service instance is healthy.
    /// </summary>
    public bool IsHealthy
    {
        get => Volatile.Read(ref _isHealthy);
        set => Volatile.Write(ref _isHealthy, value);
    }

    /// <summary>
    /// Gets the service URI.
    /// </summary>
    public Uri GetUri(string scheme = "http") => new($"{scheme}://{Host}:{Port}");
}

/// <summary>
/// Configuration options for service registry.
/// </summary>
public sealed class ServiceRegistryOptions : IValidatableOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ServiceRegistry";

    /// <summary>
    /// The heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// The service expiration time in seconds.
    /// </summary>
    public int ServiceExpireSeconds { get; set; } = 30;

    /// <summary>
    /// The health check interval in seconds.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 15;

    /// <summary>Validates the configuration options.</summary>
    public void Validate()
    {
        if (HeartbeatIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("HeartbeatIntervalSeconds must be positive");
        }

        if (ServiceExpireSeconds <= HeartbeatIntervalSeconds)
        {
            throw new InvalidOperationException(
                "ServiceExpireSeconds must be greater than HeartbeatIntervalSeconds"
            );
        }

        if (HealthCheckIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("HealthCheckIntervalSeconds must be positive");
        }
    }
}

/// <summary>
/// Defines the interface for service registration and discovery.
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Registers a service instance.
    /// </summary>
    Task RegisterAsync(ServiceInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deregisters a service instance.
    /// </summary>
    Task DeregisterAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat for the specified service instance.
    /// </summary>
    Task HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all healthy instances of the specified service.
    /// </summary>
    Task<IReadOnlyList<ServiceInstance>> GetInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all registered service names.
    /// </summary>
    Task<IReadOnlyList<string>> GetServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches for changes to the specified service.
    /// </summary>
    IAsyncEnumerable<ServiceInstance[]> WatchAsync(
        string serviceName,
        CancellationToken cancellationToken = default
    );
}
