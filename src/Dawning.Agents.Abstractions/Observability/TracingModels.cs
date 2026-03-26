namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// Represents the W3C trace context for distributed tracing.
/// </summary>
public record TraceContext
{
    /// <summary>
    /// Gets the trace ID.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Gets the span ID.
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// Gets the trace flags.
    /// </summary>
    public required string TraceFlags { get; init; }
}

/// <summary>
/// Defines the kind of a span.
/// </summary>
public enum SpanKind
{
    /// <summary>
    /// Internal operation.
    /// </summary>
    Internal,

    /// <summary>
    /// Client call.
    /// </summary>
    Client,

    /// <summary>
    /// Server-side processing.
    /// </summary>
    Server,

    /// <summary>
    /// Message producer.
    /// </summary>
    Producer,

    /// <summary>
    /// Message consumer.
    /// </summary>
    Consumer,
}

/// <summary>
/// Defines the status of a span.
/// </summary>
public enum SpanStatus
{
    /// <summary>
    /// Unset.
    /// </summary>
    Unset,

    /// <summary>
    /// OK.
    /// </summary>
    Ok,

    /// <summary>
    /// Error.
    /// </summary>
    Error,
}

/// <summary>
/// Represents a trace span for distributed tracing.
/// </summary>
public interface ITraceSpan : IDisposable
{
    /// <summary>
    /// Gets the span ID.
    /// </summary>
    string SpanId { get; }

    /// <summary>
    /// Sets an attribute on the span.
    /// </summary>
    void SetAttribute(string key, object value);

    /// <summary>
    /// Sets the span status.
    /// </summary>
    void SetStatus(SpanStatus status, string? description = null);

    /// <summary>
    /// Records an exception on the span.
    /// </summary>
    void RecordException(Exception ex);

    /// <summary>
    /// Starts a child span.
    /// </summary>
    ITraceSpan StartChildSpan(string name);
}
