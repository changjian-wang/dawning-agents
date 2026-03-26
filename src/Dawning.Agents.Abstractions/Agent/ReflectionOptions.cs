namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Reflection engine configuration.
/// </summary>
public sealed class ReflectionOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Reflection";

    /// <summary>
    /// Whether the reflection engine is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Number of consecutive failures for the same tool before triggering reflection.
    /// </summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>
    /// Maximum number of reflections (prevents infinite repair loops).
    /// </summary>
    public int MaxReflections { get; set; } = 3;

    /// <summary>
    /// Model to use for reflection (a cheaper model can be used). <c>null</c> means use the default model.
    /// </summary>
    public string? ModelOverride { get; set; }

    /// <inheritdoc />
    public void Validate()
    {
        if (FailureThreshold < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(FailureThreshold)} must be at least 1, got {FailureThreshold}"
            );
        }

        if (MaxReflections < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(MaxReflections)} must be at least 1, got {MaxReflections}"
            );
        }
    }
}
