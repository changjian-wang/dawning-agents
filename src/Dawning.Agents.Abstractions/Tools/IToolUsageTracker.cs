namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool usage tracker — records and queries tool execution statistics.
/// </summary>
public interface IToolUsageTracker
{
    /// <summary>
    /// Records a single tool execution.
    /// </summary>
    /// <param name="record">Execution record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordUsageAsync(ToolUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics for the specified tool.
    /// </summary>
    /// <param name="toolName">Tool name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ToolUsageStats> GetStatsAsync(
        string toolName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets statistics for all tools.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ToolUsageStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a list of low-utility tools (success rate below threshold).
    /// </summary>
    /// <param name="successRateThreshold">Success rate threshold.</param>
    /// <param name="minCalls">Minimum number of calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ToolUsageStats>> GetLowUtilityToolsAsync(
        float successRateThreshold = 0.3f,
        int minCalls = 3,
        CancellationToken cancellationToken = default
    );
}
