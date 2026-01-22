namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 审批结果
/// </summary>
public record ApprovalResult
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// 是否已批准
    /// </summary>
    public bool IsApproved { get; init; }

    /// <summary>
    /// 是否自动批准
    /// </summary>
    public bool IsAutoApproved { get; init; }

    /// <summary>
    /// 是否超时
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// 批准人/拒绝人
    /// </summary>
    public string? ApprovedBy { get; init; }

    /// <summary>
    /// 拒绝原因
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// 修改后的操作
    /// </summary>
    public string? ModifiedAction { get; init; }

    /// <summary>
    /// 创建自动批准结果
    /// </summary>
    public static ApprovalResult AutoApproved(string action) =>
        new()
        {
            Action = action,
            IsApproved = true,
            IsAutoApproved = true,
        };

    /// <summary>
    /// 创建批准结果
    /// </summary>
    public static ApprovalResult Approved(string action, string? approvedBy = null) =>
        new()
        {
            Action = action,
            IsApproved = true,
            ApprovedBy = approvedBy,
        };

    /// <summary>
    /// 创建拒绝结果
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
    /// 创建修改结果
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
    /// 创建超时结果
    /// </summary>
    public static ApprovalResult TimedOut(string action) =>
        new()
        {
            Action = action,
            IsApproved = false,
            IsTimedOut = true,
            RejectionReason = "审批请求超时",
        };
}
