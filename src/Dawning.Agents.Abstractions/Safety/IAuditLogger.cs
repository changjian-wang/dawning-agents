using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// Audit logger interface.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit event.
    /// </summary>
    /// <param name="entry">Audit entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries audit logs.
    /// </summary>
    /// <param name="filter">Filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit entries.</returns>
    Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Audit entry.
/// </summary>
public record AuditEntry
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Event type.
    /// </summary>
    public required AuditEventType EventType { get; init; }

    /// <summary>
    /// Session ID.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Agent name.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// User input (may be masked).
    /// </summary>
    public string? Input { get; init; }

    /// <summary>
    /// Agent output (may be masked).
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Tool name (if this is a tool call).
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Tool arguments (may be masked).
    /// </summary>
    public string? ToolArgs { get; init; }

    /// <summary>
    /// Result status.
    /// </summary>
    public AuditResultStatus Status { get; init; } = AuditResultStatus.Success;

    /// <summary>
    /// Error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Token usage.
    /// </summary>
    public int? TokensUsed { get; init; }

    /// <summary>
    /// Triggered guardrails.
    /// </summary>
    public IReadOnlyList<string>? TriggeredGuardrails { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Audit event type.
/// </summary>
public enum AuditEventType
{
    /// <summary>
    /// Agent run started.
    /// </summary>
    AgentRunStart,

    /// <summary>
    /// Agent run ended.
    /// </summary>
    AgentRunEnd,

    /// <summary>
    /// LLM call.
    /// </summary>
    LLMCall,

    /// <summary>
    /// Tool call.
    /// </summary>
    ToolCall,

    /// <summary>
    /// Guardrail triggered.
    /// </summary>
    GuardrailTriggered,

    /// <summary>
    /// Rate limited.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Handoff (task transfer).
    /// </summary>
    Handoff,
}

/// <summary>
/// Audit result status.
/// </summary>
public enum AuditResultStatus
{
    /// <summary>
    /// Success.
    /// </summary>
    Success,

    /// <summary>
    /// Failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Blocked.
    /// </summary>
    Blocked,

    /// <summary>
    /// Rate limited.
    /// </summary>
    RateLimited,
}

/// <summary>
/// Audit log filter criteria.
/// </summary>
public record AuditFilter
{
    /// <summary>
    /// Session ID.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Agent name.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Event type.
    /// </summary>
    public AuditEventType? EventType { get; init; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Result status.
    /// </summary>
    public AuditResultStatus? Status { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; init; } = 100;
}

/// <summary>
/// Audit log configuration.
/// </summary>
public class AuditOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// Enable audit logging.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Log input content.
    /// </summary>
    public bool LogInput { get; set; } = true;

    /// <summary>
    /// Log output content.
    /// </summary>
    public bool LogOutput { get; set; } = true;

    /// <summary>
    /// Log tool arguments.
    /// </summary>
    public bool LogToolArgs { get; set; } = true;

    /// <summary>
    /// Maximum content length (truncated if exceeded).
    /// </summary>
    public int MaxContentLength { get; set; } = 1000;

    /// <summary>
    /// Maximum number of in-memory entries.
    /// </summary>
    public int MaxInMemoryEntries { get; set; } = 10000;

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxContentLength <= 0)
        {
            throw new InvalidOperationException("MaxContentLength must be greater than 0");
        }

        if (MaxInMemoryEntries <= 0)
        {
            throw new InvalidOperationException("MaxInMemoryEntries must be greater than 0");
        }
    }
}
