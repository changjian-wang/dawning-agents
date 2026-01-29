namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 模型路由器接口
/// </summary>
/// <remarks>
/// 支持多种路由策略：
/// <list type="bullet">
///   <item>成本优化 - 选择最便宜的模型</item>
///   <item>延迟优化 - 选择响应最快的模型</item>
///   <item>负载均衡 - 轮询或加权分配</item>
///   <item>故障转移 - 自动切换到备用模型</item>
/// </list>
/// </remarks>
public interface IModelRouter
{
    /// <summary>
    /// 路由器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 选择最佳模型提供者
    /// </summary>
    /// <param name="context">路由上下文（包含请求信息）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选中的模型提供者</returns>
    Task<ILLMProvider> SelectProviderAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取所有可用的提供者
    /// </summary>
    IReadOnlyList<ILLMProvider> GetAvailableProviders();

    /// <summary>
    /// 报告调用结果（用于更新统计信息）
    /// </summary>
    /// <param name="provider">使用的提供者</param>
    /// <param name="result">调用结果</param>
    void ReportResult(ILLMProvider provider, ModelCallResult result);
}

/// <summary>
/// 模型路由上下文
/// </summary>
public record ModelRoutingContext
{
    /// <summary>
    /// 预估的输入 Token 数
    /// </summary>
    public int EstimatedInputTokens { get; init; }

    /// <summary>
    /// 预估的输出 Token 数
    /// </summary>
    public int EstimatedOutputTokens { get; init; }

    /// <summary>
    /// 请求优先级
    /// </summary>
    public RequestPriority Priority { get; init; } = RequestPriority.Normal;

    /// <summary>
    /// 是否需要流式响应
    /// </summary>
    public bool RequiresStreaming { get; init; }

    /// <summary>
    /// 最大延迟要求（毫秒，0 表示无限制）
    /// </summary>
    public int MaxLatencyMs { get; init; }

    /// <summary>
    /// 最大成本要求（美元，0 表示无限制）
    /// </summary>
    public decimal MaxCost { get; init; }

    /// <summary>
    /// 首选模型名称（可选）
    /// </summary>
    public string? PreferredModel { get; init; }

    /// <summary>
    /// 排除的提供者列表（故障转移时使用）
    /// </summary>
    public IReadOnlyList<string> ExcludedProviders { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 请求优先级
/// </summary>
public enum RequestPriority
{
    /// <summary>低优先级（允许排队、使用便宜模型）</summary>
    Low = 0,

    /// <summary>正常优先级</summary>
    Normal = 1,

    /// <summary>高优先级（优先处理、使用更好模型）</summary>
    High = 2,

    /// <summary>紧急（立即处理、使用最快模型）</summary>
    Critical = 3
}

/// <summary>
/// 模型调用结果
/// </summary>
public class ModelCallResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 延迟（毫秒）
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// 输入 Token 数
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// 输出 Token 数
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// 实际成本（美元）
    /// </summary>
    public decimal Cost { get; init; }

    /// <summary>
    /// 错误信息（失败时）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ModelCallResult Succeeded(long latencyMs, int inputTokens, int outputTokens, decimal cost)
        => new()
        {
            Success = true,
            LatencyMs = latencyMs,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Cost = cost
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ModelCallResult Failed(string error, long latencyMs = 0)
        => new()
        {
            Success = false,
            Error = error,
            LatencyMs = latencyMs
        };
}
