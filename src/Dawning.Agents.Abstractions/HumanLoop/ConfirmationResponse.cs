namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Human confirmation response.
/// </summary>
public record ConfirmationResponse
{
    /// <summary>
    /// Corresponding request ID.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Selected option ID.
    /// </summary>
    public required string SelectedOption { get; init; }

    /// <summary>
    /// Freeform input content (for FreeformInput type).
    /// </summary>
    public string? FreeformInput { get; init; }

    /// <summary>
    /// Modified content (for Review type).
    /// </summary>
    public string? ModifiedContent { get; init; }

    /// <summary>
    /// Response time.
    /// </summary>
    public DateTimeOffset RespondedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Responder.
    /// </summary>
    public string? RespondedBy { get; init; }

    /// <summary>
    /// Reason.
    /// </summary>
    public string? Reason { get; init; }
}
