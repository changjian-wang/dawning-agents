namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// 审计日志记录器接口
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// 记录审计事件
    /// </summary>
    /// <param name="entry">审计条目</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    /// <param name="filter">过滤条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计条目列表</returns>
    Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditFilter filter,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 审计条目
/// </summary>
public record AuditEntry
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 事件类型
    /// </summary>
    public required AuditEventType EventType { get; init; }

    /// <summary>
    /// 会话ID
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// 用户输入（可能被脱敏）
    /// </summary>
    public string? Input { get; init; }

    /// <summary>
    /// Agent 输出（可能被脱敏）
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// 工具名称（如果是工具调用）
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// 工具参数（可能被脱敏）
    /// </summary>
    public string? ToolArgs { get; init; }

    /// <summary>
    /// 结果状态
    /// </summary>
    public AuditResultStatus Status { get; init; } = AuditResultStatus.Success;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Token 使用量
    /// </summary>
    public int? TokensUsed { get; init; }

    /// <summary>
    /// 触发的护栏
    /// </summary>
    public IReadOnlyList<string>? TriggeredGuardrails { get; init; }

    /// <summary>
    /// 额外元数据
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// 审计事件类型
/// </summary>
public enum AuditEventType
{
    /// <summary>
    /// Agent 运行开始
    /// </summary>
    AgentRunStart,

    /// <summary>
    /// Agent 运行结束
    /// </summary>
    AgentRunEnd,

    /// <summary>
    /// LLM 调用
    /// </summary>
    LLMCall,

    /// <summary>
    /// 工具调用
    /// </summary>
    ToolCall,

    /// <summary>
    /// 护栏触发
    /// </summary>
    GuardrailTriggered,

    /// <summary>
    /// 速率限制
    /// </summary>
    RateLimited,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// Handoff（任务转交）
    /// </summary>
    Handoff,
}

/// <summary>
/// 审计结果状态
/// </summary>
public enum AuditResultStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 被阻止
    /// </summary>
    Blocked,

    /// <summary>
    /// 被限制
    /// </summary>
    RateLimited,
}

/// <summary>
/// 审计日志过滤条件
/// </summary>
public record AuditFilter
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public AuditEventType? EventType { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// 结果状态
    /// </summary>
    public AuditResultStatus? Status { get; init; }

    /// <summary>
    /// 最大返回数量
    /// </summary>
    public int MaxResults { get; init; } = 100;
}

/// <summary>
/// 审计日志配置
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// 启用审计日志
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 记录输入内容
    /// </summary>
    public bool LogInput { get; set; } = true;

    /// <summary>
    /// 记录输出内容
    /// </summary>
    public bool LogOutput { get; set; } = true;

    /// <summary>
    /// 记录工具参数
    /// </summary>
    public bool LogToolArgs { get; set; } = true;

    /// <summary>
    /// 最大内容长度（超过则截断）
    /// </summary>
    public int MaxContentLength { get; set; } = 1000;

    /// <summary>
    /// 内存存储最大条目数
    /// </summary>
    public int MaxInMemoryEntries { get; set; } = 10000;
}
