namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// Agent 通信的基础消息
/// </summary>
public abstract record AgentMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 发送者 Agent ID
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// 接收者 Agent ID（null 表示广播）
    /// </summary>
    public string? ReceiverId { get; init; }

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 消息元数据
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// 任务请求消息
/// </summary>
public record TaskMessage : AgentMessage
{
    /// <summary>
    /// 任务内容
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// 任务优先级（0 最高）
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 任务超时时间
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 关联 ID（用于请求/响应匹配）
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// 任务响应消息
/// </summary>
public record ResponseMessage : AgentMessage
{
    /// <summary>
    /// 关联 ID（匹配原始请求）
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// 响应结果
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// 状态更新消息
/// </summary>
public record StatusMessage : AgentMessage
{
    /// <summary>
    /// Agent 当前状态
    /// </summary>
    public required AgentStatus Status { get; init; }

    /// <summary>
    /// 当前执行的任务
    /// </summary>
    public string? CurrentTask { get; init; }

    /// <summary>
    /// 任务进度（0.0 - 1.0）
    /// </summary>
    public double? Progress { get; init; }
}

/// <summary>
/// 事件通知消息
/// </summary>
public record EventMessage : AgentMessage
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// 事件负载数据
    /// </summary>
    public required object Payload { get; init; }
}

/// <summary>
/// Agent 状态枚举
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// 空闲
    /// </summary>
    Idle,

    /// <summary>
    /// 忙碌
    /// </summary>
    Busy,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 离线
    /// </summary>
    Offline,
}
