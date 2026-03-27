using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// Gradual rollout agent — routes requests to new or old agent implementations
/// based on a feature flag percentage, with automatic rollback on failure.
/// </summary>
public sealed class GradualRolloutAgent : IAgent
{
    private readonly IAgent _stableAgent;
    private readonly IAgent _canaryAgent;
    private readonly IFeatureFlag _featureFlag;
    private readonly string _featureName;
    private readonly GradualRolloutOptions _options;
    private readonly ILogger<GradualRolloutAgent> _logger;

    // Rollback tracking
    private readonly Lock _lock = new();
    private int _canarySuccessCount;
    private int _canaryFailureCount;
    private bool _rolledBack;

    /// <inheritdoc />
    public string Name => _stableAgent.Name;

    /// <inheritdoc />
    public string Instructions => _stableAgent.Instructions;

    /// <summary>
    /// Creates a gradual rollout agent.
    /// </summary>
    /// <param name="stableAgent">The stable (existing) agent implementation.</param>
    /// <param name="canaryAgent">The canary (new) agent implementation.</param>
    /// <param name="featureFlag">Feature flag for routing.</param>
    /// <param name="featureName">Feature flag name.</param>
    /// <param name="options">Rollout options.</param>
    /// <param name="logger">The logger.</param>
    public GradualRolloutAgent(
        IAgent stableAgent,
        IAgent canaryAgent,
        IFeatureFlag featureFlag,
        string featureName,
        GradualRolloutOptions? options = null,
        ILogger<GradualRolloutAgent>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(stableAgent);
        ArgumentNullException.ThrowIfNull(canaryAgent);
        ArgumentNullException.ThrowIfNull(featureFlag);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);

        _stableAgent = stableAgent;
        _canaryAgent = canaryAgent;
        _featureFlag = featureFlag;
        _featureName = featureName;
        _options = options ?? new GradualRolloutOptions();
        _logger = logger ?? NullLogger<GradualRolloutAgent>.Instance;
    }

    /// <inheritdoc />
    public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        return RunAsync(new AgentContext { UserInput = input }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var useCanary =
            !Volatile.Read(ref _rolledBack)
            && await _featureFlag
                .IsEnabledAsync(_featureName, context.UserInput, cancellationToken)
                .ConfigureAwait(false);

        if (!useCanary)
        {
            return await _stableAgent.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var response = await _canaryAgent
                .RunAsync(context, cancellationToken)
                .ConfigureAwait(false);

            RecordCanaryResult(success: true);

            _logger.LogDebug(
                "Canary agent handled request successfully (success rate: {Rate:P0})",
                GetCanarySuccessRate()
            );

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordCanaryResult(success: false);

            _logger.LogWarning(
                ex,
                "Canary agent failed, falling back to stable agent (success rate: {Rate:P0})",
                GetCanarySuccessRate()
            );

            // Check if we should trigger automatic rollback
            CheckAutoRollback();

            // Fallback to stable agent
            return await _stableAgent.RunAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RecordCanaryResult(bool success)
    {
        lock (_lock)
        {
            if (success)
            {
                _canarySuccessCount++;
            }
            else
            {
                _canaryFailureCount++;
            }
        }
    }

    private float GetCanarySuccessRate()
    {
        lock (_lock)
        {
            var total = _canarySuccessCount + _canaryFailureCount;
            return total > 0 ? (float)_canarySuccessCount / total : 1f;
        }
    }

    private void CheckAutoRollback()
    {
        lock (_lock)
        {
            var total = _canarySuccessCount + _canaryFailureCount;
            if (total < _options.MinSamplesBeforeRollback)
            {
                return;
            }

            var successRate = (float)_canarySuccessCount / total;
            if (successRate < _options.RollbackThreshold)
            {
                Volatile.Write(ref _rolledBack, true);
                _logger.LogWarning(
                    "Automatic rollback triggered: canary success rate {Rate:P0} < threshold {Threshold:P0} after {Total} requests",
                    successRate,
                    _options.RollbackThreshold,
                    total
                );
            }
        }
    }
}

/// <summary>
/// Gradual rollout configuration.
/// </summary>
public sealed class GradualRolloutOptions
{
    /// <summary>
    /// Success rate threshold below which automatic rollback is triggered (0–1).
    /// </summary>
    public float RollbackThreshold
    {
        get => _rollbackThreshold;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0f);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1f);
            _rollbackThreshold = value;
        }
    }

    private float _rollbackThreshold = 0.7f;

    /// <summary>
    /// Minimum number of canary samples before rollback evaluation starts.
    /// </summary>
    public int MinSamplesBeforeRollback
    {
        get => _minSamplesBeforeRollback;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minSamplesBeforeRollback = value;
        }
    }

    private int _minSamplesBeforeRollback = 5;
}
