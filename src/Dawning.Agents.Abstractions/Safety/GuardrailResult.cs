namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// 护栏检查结果
/// </summary>
public record GuardrailResult
{
    /// <summary>
    /// 是否通过检查
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// 检查消息（通过时为空，失败时为原因）
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 触发的护栏名称
    /// </summary>
    public string? TriggeredBy { get; init; }

    /// <summary>
    /// 处理后的内容（可能被修改、脱敏等）
    /// </summary>
    public string? ProcessedContent { get; init; }

    /// <summary>
    /// 检测到的问题详情
    /// </summary>
    public IReadOnlyList<GuardrailIssue> Issues { get; init; } = [];

    /// <summary>
    /// 创建通过结果
    /// </summary>
    public static GuardrailResult Pass(string? processedContent = null) =>
        new() { Passed = true, ProcessedContent = processedContent };

    /// <summary>
    /// 创建通过结果（带处理后内容）
    /// </summary>
    public static GuardrailResult PassWithContent(string processedContent) =>
        new() { Passed = true, ProcessedContent = processedContent };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static GuardrailResult Fail(
        string message,
        string? triggeredBy = null,
        IReadOnlyList<GuardrailIssue>? issues = null
    ) =>
        new()
        {
            Passed = false,
            Message = message,
            TriggeredBy = triggeredBy,
            Issues = issues ?? [],
        };
}

/// <summary>
/// 护栏检测到的问题
/// </summary>
public record GuardrailIssue
{
    /// <summary>
    /// 问题类型
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// 问题描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 问题位置（如果适用）
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// 问题长度（如果适用）
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// 匹配到的内容（如果适用，可能被部分遮蔽）
    /// </summary>
    public string? MatchedContent { get; init; }

    /// <summary>
    /// 严重程度
    /// </summary>
    public IssueSeverity Severity { get; init; } = IssueSeverity.Warning;
}

/// <summary>
/// 问题严重程度
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// 信息提示
    /// </summary>
    Info,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误（阻止执行）
    /// </summary>
    Error,

    /// <summary>
    /// 严重错误
    /// </summary>
    Critical,
}
