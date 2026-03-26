using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// Approval configuration.
/// </summary>
public class ApprovalConfig
{
    /// <summary>
    /// Whether to require approval for low-risk operations.
    /// </summary>
    public bool RequireApprovalForLowRisk { get; set; } = false;

    /// <summary>
    /// Whether to require approval for medium-risk operations.
    /// </summary>
    public bool RequireApprovalForMediumRisk { get; set; } = true;

    /// <summary>
    /// Approval timeout.
    /// </summary>
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Default action on timeout (approve/reject).
    /// </summary>
    public string DefaultOnTimeout { get; set; } = "reject";
}

/// <summary>
/// Human-in-the-loop configuration.
/// </summary>
public class HumanLoopOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HumanLoop";

    /// <summary>
    /// Whether to confirm before execution.
    /// </summary>
    public bool ConfirmBeforeExecution { get; set; } = false;

    /// <summary>
    /// Whether to review before returning.
    /// </summary>
    public bool ReviewBeforeReturn { get; set; } = false;

    /// <summary>
    /// Whether to require approval for medium-risk operations.
    /// </summary>
    public bool RequireApprovalForMediumRisk { get; set; } = true;

    /// <summary>
    /// Default timeout.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum retry count.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// High-risk keywords (for automatic risk level detection).
    /// </summary>
    public string[] HighRiskKeywords { get; set; } =
    [
        "delete",
        "remove",
        "destroy",
        "execute",
        "transfer",
        "payment",
    ];

    /// <summary>
    /// Critical-risk keywords.
    /// </summary>
    public string[] CriticalRiskKeywords { get; set; } =
    ["production", "financial", "customer data", "credentials"];

    /// <inheritdoc />
    public void Validate()
    {
        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("DefaultTimeout must be greater than zero");
        }

        if (MaxRetries < 0)
        {
            throw new InvalidOperationException("MaxRetries must be non-negative");
        }
    }
}
