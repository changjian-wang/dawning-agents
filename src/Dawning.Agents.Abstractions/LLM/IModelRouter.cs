namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Model router interface.
/// </summary>
/// <remarks>
/// Supports multiple routing strategies:
/// <list type="bullet">
///   <item>Cost optimization – select the cheapest model</item>
///   <item>Latency optimization – select the fastest model</item>
///   <item>Load balancing – round-robin or weighted distribution</item>
///   <item>Failover – automatically switch to a fallback model</item>
/// </list>
/// </remarks>
public interface IModelRouter
{
    /// <summary>
    /// Router name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects the best model provider.
    /// </summary>
    /// <param name="context">Routing context containing request information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The selected model provider.</returns>
    Task<ILLMProvider> SelectProviderAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all available providers.
    /// </summary>
    IReadOnlyList<ILLMProvider> GetAvailableProviders();

    /// <summary>
    /// Reports a call result (used to update statistics).
    /// </summary>
    /// <param name="provider">The provider used.</param>
    /// <param name="result">The call result.</param>
    void ReportResult(ILLMProvider provider, ModelCallResult result);
}

/// <summary>
/// Model routing context.
/// </summary>
public record ModelRoutingContext
{
    /// <summary>
    /// Estimated number of input tokens.
    /// </summary>
    public int EstimatedInputTokens { get; init; }

    /// <summary>
    /// Estimated number of output tokens.
    /// </summary>
    public int EstimatedOutputTokens { get; init; }

    /// <summary>
    /// Request priority.
    /// </summary>
    public RequestPriority Priority { get; init; } = RequestPriority.Normal;

    /// <summary>
    /// Whether streaming response is required.
    /// </summary>
    public bool RequiresStreaming { get; init; }

    /// <summary>
    /// Maximum latency requirement in milliseconds (0 means no limit).
    /// </summary>
    public int MaxLatencyMs { get; init; }

    /// <summary>
    /// Maximum cost requirement in USD (0 means no limit).
    /// </summary>
    public decimal MaxCost { get; init; }

    /// <summary>
    /// Preferred model name (optional).
    /// </summary>
    public string? PreferredModel { get; init; }

    /// <summary>
    /// List of excluded providers (used during failover).
    /// </summary>
    public IReadOnlyList<string> ExcludedProviders { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Request priority.
/// </summary>
public enum RequestPriority
{
    /// <summary>Low priority (allows queuing; uses cheaper models).</summary>
    Low = 0,

    /// <summary>Normal priority.</summary>
    Normal = 1,

    /// <summary>High priority (prioritized processing; uses better models).</summary>
    High = 2,

    /// <summary>Critical (immediate processing; uses the fastest model).</summary>
    Critical = 3,
}

/// <summary>
/// Model call result.
/// </summary>
public class ModelCallResult
{
    /// <summary>
    /// Whether the call succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// Number of input tokens.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Actual cost in USD.
    /// </summary>
    public decimal Cost { get; init; }

    /// <summary>
    /// Error message (on failure).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static ModelCallResult Succeeded(
        long latencyMs,
        int inputTokens,
        int outputTokens,
        decimal cost
    ) =>
        new()
        {
            Success = true,
            LatencyMs = latencyMs,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Cost = cost,
        };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static ModelCallResult Failed(string error, long latencyMs = 0) =>
        new()
        {
            Success = false,
            Error = error,
            LatencyMs = latencyMs,
        };
}
