namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Request to escalate to human handling.
/// </summary>
public record EscalationRequest
{
    /// <summary>
    /// Unique request identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Escalation reason.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Detailed description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Escalation severity.
    /// </summary>
    public EscalationSeverity Severity { get; init; } = EscalationSeverity.Medium;

    /// <summary>
    /// Agent name.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Task ID.
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// Context data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Attempted solutions.
    /// </summary>
    public IReadOnlyList<string> AttemptedSolutions { get; init; } = [];

    /// <summary>
    /// Creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Escalation severity.
/// </summary>
public enum EscalationSeverity
{
    /// <summary>
    /// Low - informational escalation.
    /// </summary>
    Low,

    /// <summary>
    /// Medium - requires attention.
    /// </summary>
    Medium,

    /// <summary>
    /// High - requires immediate action.
    /// </summary>
    High,

    /// <summary>
    /// Critical - urgent action required.
    /// </summary>
    Critical,
}

/// <summary>
/// Escalation result.
/// </summary>
public record EscalationResult
{
    /// <summary>
    /// Corresponding request ID.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Action taken.
    /// </summary>
    public EscalationAction Action { get; init; }

    /// <summary>
    /// Resolution.
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Instructions.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Resolver.
    /// </summary>
    public string? ResolvedBy { get; init; }

    /// <summary>
    /// Resolution time.
    /// </summary>
    public DateTimeOffset ResolvedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Escalation action type.
/// </summary>
public enum EscalationAction
{
    /// <summary>
    /// Resolved.
    /// </summary>
    Resolved,

    /// <summary>
    /// Skipped.
    /// </summary>
    Skipped,

    /// <summary>
    /// Aborted.
    /// </summary>
    Aborted,

    /// <summary>
    /// Delegated.
    /// </summary>
    Delegated,

    /// <summary>
    /// Retried.
    /// </summary>
    Retried,
}
