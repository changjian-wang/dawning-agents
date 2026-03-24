namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具使用追踪器 — 记录和查询工具执行统计
/// </summary>
public interface IToolUsageTracker
{
    /// <summary>
    /// 记录一次工具执行
    /// </summary>
    /// <param name="record">执行记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RecordUsageAsync(ToolUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定工具的统计
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ToolUsageStats> GetStatsAsync(
        string toolName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取所有工具的统计
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ToolUsageStats>> GetAllStatsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取低效用工具列表（成功率低于阈值）
    /// </summary>
    /// <param name="successRateThreshold">成功率阈值</param>
    /// <param name="minCalls">最小调用次数</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<ToolUsageStats>> GetLowUtilityToolsAsync(
        float successRateThreshold = 0.3f,
        int minCalls = 3,
        CancellationToken cancellationToken = default
    );
}
