namespace Dawning.Agents.Abstractions.Scaling;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Represents a work item queued for agent processing.
/// </summary>
public record AgentWorkItem
{
    /// <summary>
    /// Gets the work item ID.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the input content.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Gets the agent context.
    /// </summary>
    public AgentContext? Context { get; init; }

    /// <summary>
    /// Gets the completion source for signaling the result.
    /// </summary>
    public required TaskCompletionSource<AgentResponse> CompletionSource { get; init; }

    /// <summary>
    /// Gets the time the item was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the priority of the work item.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Represents an agent instance used for load balancing and scaling.
/// </summary>
public class AgentInstance
{
    /// <summary>
    /// Gets the instance ID.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the agent instance (local mode).
    /// </summary>
    public IAgent? Agent { get; init; }

    /// <summary>
    /// Gets the endpoint address (distributed mode).
    /// </summary>
    public string Endpoint { get; init; } = "";

    /// <summary>
    /// Gets the service name.
    /// </summary>
    public string ServiceName { get; init; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether the instance is healthy.
    /// </summary>
    public bool IsHealthy
    {
        get => Volatile.Read(ref _isHealthy);
        set => Volatile.Write(ref _isHealthy, value);
    }

    private bool _isHealthy = true;

    private int _activeRequests;

    /// <summary>
    /// Gets or sets the number of active requests.
    /// </summary>
    public int ActiveRequests
    {
        get => Volatile.Read(ref _activeRequests);
        set => Volatile.Write(ref _activeRequests, value);
    }

    /// <summary>
    /// Atomically increments the active request count.
    /// </summary>
    public void IncrementActiveRequests() => Interlocked.Increment(ref _activeRequests);

    /// <summary>
    /// Atomically decrements the active request count.
    /// </summary>
    public void DecrementActiveRequests() => Interlocked.Decrement(ref _activeRequests);

    /// <summary>
    /// Gets the weight (used for weighted load balancing).
    /// </summary>
    public int Weight { get; init; } = 100;

    /// <summary>
    /// Gets or sets the time of the last health check.
    /// </summary>
    public DateTimeOffset LastHealthCheck { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the tags associated with this instance.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Provides a queue for agent work items.
/// </summary>
public interface IAgentRequestQueue
{
    /// <summary>
    /// Enqueues a work item.
    /// </summary>
    ValueTask EnqueueAsync(AgentWorkItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues a work item.
    /// </summary>
    ValueTask<AgentWorkItem?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current queue length.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets a value indicating whether the queue can accept more items.
    /// </summary>
    bool CanWrite { get; }
}

/// <summary>
/// Manages a pool of agent workers.
/// </summary>
public interface IAgentWorkerPool : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Starts the worker pool.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the worker pool.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of workers.
    /// </summary>
    int WorkerCount { get; }

    /// <summary>
    /// Gets a value indicating whether the pool is running.
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// Distributes requests across agent instances.
/// </summary>
public interface IAgentLoadBalancer
{
    /// <summary>
    /// Registers an agent instance.
    /// </summary>
    void RegisterInstance(AgentInstance instance);

    /// <summary>
    /// Unregisters an agent instance.
    /// </summary>
    void UnregisterInstance(string instanceId);

    /// <summary>
    /// Gets the next instance using round-robin selection.
    /// </summary>
    AgentInstance? GetNextInstance();

    /// <summary>
    /// Gets the instance with the least load.
    /// </summary>
    AgentInstance? GetLeastLoadedInstance();

    /// <summary>
    /// Gets all registered instances.
    /// </summary>
    IReadOnlyList<AgentInstance> GetAllInstances();

    /// <summary>
    /// Gets the number of healthy instances.
    /// </summary>
    int HealthyInstanceCount { get; }
}

/// <summary>
/// Provides circuit breaker functionality for fault tolerance.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    CircuitState State { get; }

    /// <summary>
    /// Executes an action with circuit breaker protection.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with circuit breaker protection (no return value).
    /// </summary>
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the circuit breaker.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the consecutive failure count.
    /// </summary>
    int FailureCount { get; }
}

/// <summary>
/// Provides automatic scaling of agent instances.
/// </summary>
public interface IAgentAutoScaler
{
    /// <summary>
    /// Evaluates and applies a scaling decision.
    /// </summary>
    Task<ScalingDecision> EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of instances.
    /// </summary>
    int CurrentInstances { get; }

    /// <summary>
    /// Gets the time of the last scale-up event.
    /// </summary>
    DateTimeOffset? LastScaleUpTime { get; }

    /// <summary>
    /// Gets the time of the last scale-down event.
    /// </summary>
    DateTimeOffset? LastScaleDownTime { get; }
}
