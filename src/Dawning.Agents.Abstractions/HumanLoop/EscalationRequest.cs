namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 升级到人工处理的请求
/// </summary>
public record EscalationRequest
{
    /// <summary>
    /// 唯一请求标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 升级原因
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 详细描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 升级严重程度
    /// </summary>
    public EscalationSeverity Severity { get; init; } = EscalationSeverity.Medium;

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// 任务 ID
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// 上下文数据
    /// </summary>
    public IDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 已尝试的解决方案
    /// </summary>
    public IReadOnlyList<string> AttemptedSolutions { get; init; } = [];

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 升级严重程度
/// </summary>
public enum EscalationSeverity
{
    /// <summary>
    /// 低 - 信息性升级
    /// </summary>
    Low,

    /// <summary>
    /// 中等 - 需要关注
    /// </summary>
    Medium,

    /// <summary>
    /// 高 - 需要立即处理
    /// </summary>
    High,

    /// <summary>
    /// 关键 - 紧急处理
    /// </summary>
    Critical,
}

/// <summary>
/// 升级结果
/// </summary>
public record EscalationResult
{
    /// <summary>
    /// 对应的请求 ID
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// 采取的操作
    /// </summary>
    public EscalationAction Action { get; init; }

    /// <summary>
    /// 解决方案
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// 指令
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// 解决人
    /// </summary>
    public string? ResolvedBy { get; init; }

    /// <summary>
    /// 解决时间
    /// </summary>
    public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 升级操作类型
/// </summary>
public enum EscalationAction
{
    /// <summary>
    /// 已解决
    /// </summary>
    Resolved,

    /// <summary>
    /// 已跳过
    /// </summary>
    Skipped,

    /// <summary>
    /// 已中止
    /// </summary>
    Aborted,

    /// <summary>
    /// 已委派
    /// </summary>
    Delegated,

    /// <summary>
    /// 重试
    /// </summary>
    Retried,
}
