namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// Base message for agent communication.
/// </summary>
public abstract record AgentMessage
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Sender agent ID.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Receiver agent ID. <see langword="null"/> indicates a broadcast message.
    /// </summary>
    public string? ReceiverId { get; init; }

    /// <summary>
    /// Message timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Message metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Task request message.
/// </summary>
public record TaskMessage : AgentMessage
{
    /// <summary>
    /// Task content.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Task priority (0 is highest).
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Task timeout duration.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Correlation ID for request/response matching.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Task response message.
/// </summary>
public record ResponseMessage : AgentMessage
{
    /// <summary>
    /// Correlation ID matching the original request.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Response result.
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Status update message.
/// </summary>
public record StatusMessage : AgentMessage
{
    /// <summary>
    /// Current agent status.
    /// </summary>
    public required AgentStatus Status { get; init; }

    /// <summary>
    /// Currently executing task.
    /// </summary>
    public string? CurrentTask { get; init; }

    /// <summary>
    /// Task progress (0.0 - 1.0).
    /// </summary>
    public double? Progress { get; init; }
}

/// <summary>
/// Event notification message.
/// </summary>
public record EventMessage : AgentMessage
{
    /// <summary>
    /// Event type.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Event payload data.
    /// </summary>
    public required object Payload { get; init; }
}

/// <summary>
/// Agent status enumeration.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Idle.
    /// </summary>
    Idle,

    /// <summary>
    /// Busy.
    /// </summary>
    Busy,

    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Offline.
    /// </summary>
    Offline,
}
