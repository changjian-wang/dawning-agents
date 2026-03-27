namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// Exception thrown when an agent escalates to human handling.
/// </summary>
public class AgentEscalationException : Exception
{
    /// <summary>
    /// Gets the escalation reason.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the detailed description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the context data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; }

    /// <summary>
    /// Gets the list of attempted solutions.
    /// </summary>
    public IReadOnlyList<string> AttemptedSolutions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEscalationException"/> class.
    /// </summary>
    public AgentEscalationException(
        string reason,
        string description,
        IReadOnlyDictionary<string, object>? context = null,
        IReadOnlyList<string>? attemptedSolutions = null
    )
        : base(reason)
    {
        Reason = reason;
        Description = description;
        Context = context ?? new Dictionary<string, object>();
        AttemptedSolutions = attemptedSolutions ?? [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEscalationException"/> class with an inner exception.
    /// </summary>
    public AgentEscalationException(
        string reason,
        string description,
        Exception innerException,
        IReadOnlyDictionary<string, object>? context = null,
        IReadOnlyList<string>? attemptedSolutions = null
    )
        : base(reason, innerException)
    {
        Reason = reason;
        Description = description;
        Context = context ?? new Dictionary<string, object>();
        AttemptedSolutions = attemptedSolutions ?? [];
    }
}
