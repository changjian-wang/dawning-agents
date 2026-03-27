namespace Dawning.Agents.Abstractions.Resilience;

/// <summary>
/// Feature flag interface — enables gradual rollout and A/B routing of agent implementations.
/// </summary>
public interface IFeatureFlag
{
    /// <summary>
    /// Checks whether a feature is enabled.
    /// </summary>
    /// <param name="featureName">Feature name.</param>
    /// <param name="context">Optional context for percentage-based evaluation (e.g., user ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the feature is enabled; otherwise <c>false</c>.</returns>
    Task<bool> IsEnabledAsync(
        string featureName,
        string? context = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Feature flag definition.
/// </summary>
public record FeatureFlagDefinition
{
    /// <summary>
    /// Feature name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the feature is globally enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Rollout percentage (0–100). Used when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public int RolloutPercentage { get; init; } = 100;
}
