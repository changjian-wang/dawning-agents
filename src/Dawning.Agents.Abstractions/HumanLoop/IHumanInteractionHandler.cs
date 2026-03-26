namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Notification level.
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// Information.
    /// </summary>
    Info,

    /// <summary>
    /// Warning.
    /// </summary>
    Warning,

    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Success.
    /// </summary>
    Success,
}

/// <summary>
/// Human interaction handler interface.
/// </summary>
public interface IHumanInteractionHandler
{
    /// <summary>
    /// Requests human confirmation.
    /// </summary>
    /// <param name="request">Confirmation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation response.</returns>
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Requests human input/feedback.
    /// </summary>
    /// <param name="prompt">Prompt message.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User input.</returns>
    Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Notifies a human (no response required).
    /// </summary>
    /// <param name="message">Message content.</param>
    /// <param name="level">Notification level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Escalates to human handling.
    /// </summary>
    /// <param name="request">Escalation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Escalation result.</returns>
    Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    );
}
