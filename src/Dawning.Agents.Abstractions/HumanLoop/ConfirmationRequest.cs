namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Human confirmation request.
/// </summary>
public record ConfirmationRequest
{
    /// <summary>
    /// Unique request identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Required confirmation type.
    /// </summary>
    public ConfirmationType Type { get; init; } = ConfirmationType.Binary;

    /// <summary>
    /// Action to confirm.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Detailed description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Risk level of the operation.
    /// </summary>
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Medium;

    /// <summary>
    /// Options for human selection.
    /// </summary>
    public IReadOnlyList<ConfirmationOption> Options { get; init; } = [];

    /// <summary>
    /// Context data for decision-making.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Request creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Confirmation timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Default action on timeout.
    /// </summary>
    public string? DefaultOnTimeout { get; init; }
}

/// <summary>
/// Confirmation type.
/// </summary>
public enum ConfirmationType
{
    /// <summary>
    /// Yes/No binary choice.
    /// </summary>
    Binary,

    /// <summary>
    /// Multiple choice.
    /// </summary>
    MultiChoice,

    /// <summary>
    /// Freeform user input.
    /// </summary>
    FreeformInput,

    /// <summary>
    /// Review and modify.
    /// </summary>
    Review,
}

/// <summary>
/// Risk level.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk - typically auto-approved.
    /// </summary>
    Low,

    /// <summary>
    /// Medium risk - may require confirmation.
    /// </summary>
    Medium,

    /// <summary>
    /// High risk - requires confirmation.
    /// </summary>
    High,

    /// <summary>
    /// Critical risk - must be confirmed.
    /// </summary>
    Critical,
}

/// <summary>
/// Confirmation option.
/// </summary>
public record ConfirmationOption
{
    /// <summary>
    /// Option unique identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Option display label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Option description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this is the default option.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Whether this is a dangerous operation.
    /// </summary>
    public bool IsDangerous { get; init; }
}
