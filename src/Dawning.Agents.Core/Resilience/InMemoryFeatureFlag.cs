using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// In-memory feature flag implementation with percentage-based rollout.
/// </summary>
public sealed class InMemoryFeatureFlag : IFeatureFlag
{
    private readonly ConcurrentDictionary<string, FeatureFlagDefinition> _flags = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly ILogger<InMemoryFeatureFlag> _logger;

    /// <summary>
    /// Creates an in-memory feature flag store.
    /// </summary>
    public InMemoryFeatureFlag(ILogger<InMemoryFeatureFlag>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryFeatureFlag>.Instance;
    }

    /// <summary>
    /// Registers or updates a feature flag definition.
    /// </summary>
    /// <param name="definition">Feature flag definition.</param>
    public void SetFlag(FeatureFlagDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _flags[definition.Name] = definition;
        _logger.LogDebug(
            "Feature flag '{Name}' set: enabled={Enabled}, rollout={Rollout}%",
            definition.Name,
            definition.Enabled,
            definition.RolloutPercentage
        );
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(
        string featureName,
        string? context = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        if (!_flags.TryGetValue(featureName, out var flag))
        {
            return Task.FromResult(false);
        }

        if (!flag.Enabled)
        {
            return Task.FromResult(false);
        }

        if (flag.RolloutPercentage >= 100)
        {
            return Task.FromResult(true);
        }

        if (flag.RolloutPercentage <= 0)
        {
            return Task.FromResult(false);
        }

        // Deterministic hash-based percentage routing
        var bucket = GetBucket(featureName, context ?? "default");
        var enabled = bucket < flag.RolloutPercentage;

        _logger.LogDebug(
            "Feature '{Name}' for context '{Context}': bucket={Bucket}, threshold={Threshold}, enabled={Enabled}",
            featureName,
            context,
            bucket,
            flag.RolloutPercentage,
            enabled
        );

        return Task.FromResult(enabled);
    }

    /// <summary>
    /// Deterministic bucket assignment (0–99) based on feature name + context.
    /// </summary>
    private static int GetBucket(string featureName, string context)
    {
        var input = $"{featureName}:{context}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100);
    }
}
