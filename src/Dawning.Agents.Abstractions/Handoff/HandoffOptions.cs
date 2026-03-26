using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff configuration options.
/// </summary>
public class HandoffOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Handoff";

    /// <summary>
    /// Maximum handoff depth (prevents infinite loops).
    /// </summary>
    /// <remarks>Defaults to 5, meaning a maximum of 5 consecutive handoffs.</remarks>
    public int MaxHandoffDepth { get; set; } = 5;

    /// <summary>
    /// Per-handoff timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Total timeout in seconds.
    /// </summary>
    public int TotalTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to allow cycles (Agent A -> B -> A).
    /// </summary>
    public bool AllowCycles { get; set; } = false;

    /// <summary>
    /// Whether to fall back to the source Agent on handoff failure.
    /// </summary>
    public bool FallbackToSource { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (MaxHandoffDepth < 1)
        {
            throw new InvalidOperationException("MaxHandoffDepth must be at least 1");
        }

        if (TimeoutSeconds < 1)
        {
            throw new InvalidOperationException("TimeoutSeconds must be at least 1");
        }

        if (TotalTimeoutSeconds < TimeoutSeconds)
        {
            throw new InvalidOperationException("TotalTimeoutSeconds must be >= TimeoutSeconds");
        }
    }
}
