namespace Dawning.Agents.Abstractions.Telemetry;

/// <summary>
/// Token 使用追踪器接口
/// </summary>
/// <remarks>
/// 用于记录和统计 LLM 调用的 Token 使用情况。
/// 支持按来源（Agent）、模型、会话等维度进行分组统计。
///
/// 使用示例:
/// <code>
/// // 记录 Token 使用
/// tracker.Record(TokenUsageRecord.Create("MyAgent", 100, 50, "gpt-4"));
///
/// // 获取统计
/// var summary = tracker.GetSummary();
/// Console.WriteLine($"总计: {summary.TotalTokens} tokens");
/// </code>
/// </remarks>
public interface ITokenUsageTracker
{
    /// <summary>
    /// 记录一次 Token 使用
    /// </summary>
    /// <param name="record">Token 使用记录</param>
    void Record(TokenUsageRecord record);

    /// <summary>
    /// 记录一次 Token 使用（简化方法）
    /// </summary>
    /// <param name="source">来源标识</param>
    /// <param name="promptTokens">输入 Token 数</param>
    /// <param name="completionTokens">输出 Token 数</param>
    /// <param name="model">模型名称</param>
    /// <param name="sessionId">会话 ID</param>
    void Record(
        string source,
        int promptTokens,
        int completionTokens,
        string? model = null,
        string? sessionId = null
    );

    /// <summary>
    /// 获取汇总统计
    /// </summary>
    /// <param name="source">按来源过滤（可选）</param>
    /// <param name="sessionId">按会话过滤（可选）</param>
    /// <returns>Token 使用汇总</returns>
    TokenUsageSummary GetSummary(string? source = null, string? sessionId = null);

    /// <summary>
    /// 获取所有记录
    /// </summary>
    /// <param name="source">按来源过滤（可选）</param>
    /// <param name="sessionId">按会话过滤（可选）</param>
    /// <returns>Token 使用记录列表</returns>
    IReadOnlyList<TokenUsageRecord> GetRecords(string? source = null, string? sessionId = null);

    /// <summary>
    /// 重置统计
    /// </summary>
    /// <param name="source">按来源重置（可选，null 表示全部重置）</param>
    /// <param name="sessionId">按会话重置（可选）</param>
    void Reset(string? source = null, string? sessionId = null);

    /// <summary>
    /// 总输入 Token 数
    /// </summary>
    int TotalPromptTokens { get; }

    /// <summary>
    /// 总输出 Token 数
    /// </summary>
    int TotalCompletionTokens { get; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    int TotalTokens { get; }

    /// <summary>
    /// 总调用次数
    /// </summary>
    int CallCount { get; }
}
