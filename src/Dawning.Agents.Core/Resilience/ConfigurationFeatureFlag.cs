using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// Configuration-backed feature flag implementation.
/// Reads feature flags from appsettings.json (supports hot reload via <see cref="IConfiguration"/>).
/// </summary>
/// <remarks>
/// Configuration format:
/// <code>
/// {
///   "FeatureFlags": {
///     "NewAgent": {
///       "Enabled": true,
///       "RolloutPercentage": 50
///     }
///   }
/// }
/// </code>
/// </remarks>
public sealed class ConfigurationFeatureFlag : IFeatureFlag
{
    private const string SectionName = "FeatureFlags";

    private readonly IConfiguration _configuration;
    private readonly InMemoryFeatureFlag _inner;
    private readonly ILogger<ConfigurationFeatureFlag> _logger;

    /// <summary>
    /// Creates a configuration-backed feature flag.
    /// </summary>
    /// <param name="configuration">The configuration instance (supports hot reload).</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationFeatureFlag(
        IConfiguration configuration,
        ILogger<ConfigurationFeatureFlag>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _inner = new InMemoryFeatureFlag(loggerFactory?.CreateLogger<InMemoryFeatureFlag>());
        _logger = logger ?? NullLogger<ConfigurationFeatureFlag>.Instance;

        LoadFlags();
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync(
        string featureName,
        string? context = null,
        CancellationToken cancellationToken = default
    )
    {
        // Reload from configuration on every check to support hot reload
        LoadFlags();
        return _inner.IsEnabledAsync(featureName, context, cancellationToken);
    }

    private void LoadFlags()
    {
        var section = _configuration.GetSection(SectionName);
        var currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                var name = child.Key;

                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning(
                        "Feature flag with empty name found in configuration, skipping"
                    );
                    continue;
                }

                var enabled = child.GetValue<bool>("Enabled");
                var rollout = child.GetValue("RolloutPercentage", 100);

                if (rollout < 0 || rollout > 100)
                {
                    _logger.LogWarning(
                        "Feature flag '{Name}' has invalid RolloutPercentage {Value}, skipping",
                        name,
                        rollout
                    );
                    continue;
                }

                currentNames.Add(name);
                _inner.SetFlag(
                    new FeatureFlagDefinition
                    {
                        Name = name,
                        Enabled = enabled,
                        RolloutPercentage = rollout,
                    }
                );
            }
        }

        _inner.RemoveFlagsNotIn(currentNames);
    }
}
