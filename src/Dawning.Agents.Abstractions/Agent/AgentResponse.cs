namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 执行响应
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// 是否执行成功
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// 最终答案
    /// </summary>
    public string? FinalAnswer { get; init; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 导致失败的异常（如果有）
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 执行的所有步骤
    /// </summary>
    public IReadOnlyList<AgentStep> Steps { get; init; } = [];

    /// <summary>
    /// 总执行时间
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// 总成本（USD）
    /// </summary>
    public decimal TotalCost { get; init; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static AgentResponse Successful(
        string finalAnswer,
        IReadOnlyList<AgentStep> steps,
        TimeSpan duration
    ) =>
        new()
        {
            Success = true,
            FinalAnswer = finalAnswer,
            Steps = steps,
            Duration = duration,
            TotalCost = steps.Sum(s => s.Cost),
        };

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static AgentResponse Failed(
        string error,
        IReadOnlyList<AgentStep> steps,
        TimeSpan duration,
        Exception? exception = null
    ) =>
        new()
        {
            Success = false,
            Error = error,
            Steps = steps,
            Duration = duration,
            TotalCost = steps.Sum(s => s.Cost),
            Exception = exception,
        };
}
