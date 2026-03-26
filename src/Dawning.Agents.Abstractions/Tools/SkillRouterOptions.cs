namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Semantic skill router configuration.
/// </summary>
public sealed class SkillRouterOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SkillRouter";

    /// <summary>
    /// Tool count threshold to enable semantic routing (tools below this count are fully injected).
    /// </summary>
    public int ActivationThreshold { get; set; } = 10;

    /// <summary>
    /// Default top-K.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Default minimum similarity.
    /// </summary>
    public float DefaultMinScore { get; set; } = 0.3f;

    /// <inheritdoc />
    public void Validate()
    {
        if (ActivationThreshold < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(ActivationThreshold)} must be at least 1, got {ActivationThreshold}"
            );
        }

        if (DefaultTopK < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(DefaultTopK)} must be at least 1, got {DefaultTopK}"
            );
        }

        if (DefaultMinScore is < 0f or > 1f)
        {
            throw new InvalidOperationException(
                $"{nameof(DefaultMinScore)} must be between 0 and 1, got {DefaultMinScore}"
            );
        }
    }
}
