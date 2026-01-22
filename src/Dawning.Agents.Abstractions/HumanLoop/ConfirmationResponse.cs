namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 人工确认响应
/// </summary>
public record ConfirmationResponse
{
    /// <summary>
    /// 对应的请求 ID
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// 选择的选项 ID
    /// </summary>
    public required string SelectedOption { get; init; }

    /// <summary>
    /// 自由输入内容（用于 FreeformInput 类型）
    /// </summary>
    public string? FreeformInput { get; init; }

    /// <summary>
    /// 修改后的内容（用于 Review 类型）
    /// </summary>
    public string? ModifiedContent { get; init; }

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTime RespondedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 响应人
    /// </summary>
    public string? RespondedBy { get; init; }

    /// <summary>
    /// 原因说明
    /// </summary>
    public string? Reason { get; init; }
}
