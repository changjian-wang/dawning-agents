namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Approval result.
/// </summary>
public record ApprovalResult
{
    /// <summary>
    /// Action name.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Whether it is approved.
    /// </summary>
    public bool IsApproved { get; init; }

    /// <summary>
    /// Whether it was auto-approved.
    /// </summary>
    public bool IsAutoApproved { get; init; }

    /// <summary>
    /// Whether it timed out.
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// Approver/rejector.
    /// </summary>
    public string? ApprovedBy { get; init; }

    /// <summary>
    /// Rejection reason.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Modified action.
    /// </summary>
    public string? ModifiedAction { get; init; }

    /// <summary>
    /// Creates an auto-approved result.
    /// </summary>
    public static ApprovalResult AutoApproved(string action) =>
        new()
        {
            Action = action,
            IsApproved = true,
            IsAutoApproved = true,
        };

    /// <summary>
    /// Creates an approved result.
    /// </summary>
    public static ApprovalResult Approved(string action, string? approvedBy = null) =>
        new()
        {
            Action = action,
            IsApproved = true,
            ApprovedBy = approvedBy,
        };

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static ApprovalResult Rejected(
        string action,
        string? reason = null,
        string? rejectedBy = null
    ) =>
        new()
        {
            Action = action,
            IsApproved = false,
            RejectionReason = reason,
            ApprovedBy = rejectedBy,
        };

    /// <summary>
    /// Creates a modified result.
    /// </summary>
    public static ApprovalResult Modified(
        string action,
        string? modifiedAction,
        string? modifiedBy = null
    ) =>
        new()
        {
            Action = action,
            IsApproved = true,
            ModifiedAction = modifiedAction,
            ApprovedBy = modifiedBy,
        };

    /// <summary>
    /// Creates a timed-out result.
    /// </summary>
    public static ApprovalResult TimedOut(string action) =>
        new()
        {
            Action = action,
            IsApproved = false,
            IsTimedOut = true,
            RejectionReason = "Approval request timed out",
        };
}
