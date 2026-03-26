namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// Guardrail check result.
/// </summary>
public record GuardrailResult
{
    /// <summary>
    /// Whether the check passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Check message (empty when passed, contains the reason when failed).
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Name of the triggered guardrail.
    /// </summary>
    public string? TriggeredBy { get; init; }

    /// <summary>
    /// Processed content (may be modified, masked, etc.).
    /// </summary>
    public string? ProcessedContent { get; init; }

    /// <summary>
    /// Details of detected issues.
    /// </summary>
    public IReadOnlyList<GuardrailIssue> Issues { get; init; } = [];

    /// <summary>
    /// Creates a passing result.
    /// </summary>
    public static GuardrailResult Pass(string? processedContent = null) =>
        new() { Passed = true, ProcessedContent = processedContent };

    /// <summary>
    /// Creates a passing result with processed content.
    /// </summary>
    public static GuardrailResult PassWithContent(string processedContent) =>
        new() { Passed = true, ProcessedContent = processedContent };

    /// <summary>
    /// Creates a failing result.
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
/// Issue detected by a guardrail.
/// </summary>
public record GuardrailIssue
{
    /// <summary>
    /// Issue type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Issue description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Issue position (if applicable).
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// Issue length (if applicable).
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// Matched content (if applicable, may be partially masked).
    /// </summary>
    public string? MatchedContent { get; init; }

    /// <summary>
    /// Severity level.
    /// </summary>
    public IssueSeverity Severity { get; init; } = IssueSeverity.Warning;
}

/// <summary>
/// Issue severity level.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational.
    /// </summary>
    Info,

    /// <summary>
    /// Warning.
    /// </summary>
    Warning,

    /// <summary>
    /// Error (blocks execution).
    /// </summary>
    Error,

    /// <summary>
    /// Critical error.
    /// </summary>
    Critical,
}
