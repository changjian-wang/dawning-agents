namespace Dawning.Agents.Abstractions.Telemetry;

/// <summary>
/// Token 使用记录
/// </summary>
/// <param name="Source">来源标识（Agent 名称、组件名等）</param>
/// <param name="PromptTokens">输入 Token 数</param>
/// <param name="CompletionTokens">输出 Token 数</param>
/// <param name="Timestamp">记录时间</param>
/// <param name="Model">使用的模型名称</param>
/// <param name="SessionId">会话 ID（可选，用于分组统计）</param>
/// <param name="Metadata">附加元数据</param>
public record TokenUsageRecord(
    string Source,
    int PromptTokens,
    int CompletionTokens,
    DateTime Timestamp,
    string? Model = null,
    string? SessionId = null,
    IReadOnlyDictionary<string, object>? Metadata = null
)
{
    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// 创建一个新的 Token 使用记录
    /// </summary>
    public static TokenUsageRecord Create(
        string source,
        int promptTokens,
        int completionTokens,
        string? model = null,
        string? sessionId = null,
        IReadOnlyDictionary<string, object>? metadata = null
    )
    {
        return new TokenUsageRecord(
            source,
            promptTokens,
            completionTokens,
            DateTime.UtcNow,
            model,
            sessionId,
            metadata
        );
    }
}

/// <summary>
/// Token 使用汇总统计
/// </summary>
/// <param name="TotalPromptTokens">总输入 Token 数</param>
/// <param name="TotalCompletionTokens">总输出 Token 数</param>
/// <param name="CallCount">调用次数</param>
/// <param name="BySource">按来源分组的统计</param>
/// <param name="ByModel">按模型分组的统计</param>
/// <param name="BySession">按会话分组的统计</param>
public record TokenUsageSummary(
    int TotalPromptTokens,
    int TotalCompletionTokens,
    int CallCount,
    IReadOnlyDictionary<string, SourceUsage> BySource,
    IReadOnlyDictionary<string, int>? ByModel = null,
    IReadOnlyDictionary<string, int>? BySession = null
)
{
    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => TotalPromptTokens + TotalCompletionTokens;

    /// <summary>
    /// 空统计
    /// </summary>
    public static TokenUsageSummary Empty => new(0, 0, 0, new Dictionary<string, SourceUsage>());
}

/// <summary>
/// 单个来源的 Token 使用统计
/// </summary>
/// <param name="PromptTokens">输入 Token 数</param>
/// <param name="CompletionTokens">输出 Token 数</param>
/// <param name="CallCount">调用次数</param>
public record SourceUsage(int PromptTokens, int CompletionTokens, int CallCount)
{
    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
