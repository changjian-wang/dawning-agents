namespace Dawning.Agents.Abstractions.Observability;

/// <summary>
/// 追踪上下文
/// </summary>
public record TraceContext
{
    /// <summary>
    /// 追踪 ID
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Span ID
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// 追踪标志
    /// </summary>
    public required string TraceFlags { get; init; }
}

/// <summary>
/// Span 类型
/// </summary>
public enum SpanKind
{
    /// <summary>
    /// 内部操作
    /// </summary>
    Internal,

    /// <summary>
    /// 客户端调用
    /// </summary>
    Client,

    /// <summary>
    /// 服务端处理
    /// </summary>
    Server,

    /// <summary>
    /// 消息生产
    /// </summary>
    Producer,

    /// <summary>
    /// 消息消费
    /// </summary>
    Consumer,
}

/// <summary>
/// Span 状态
/// </summary>
public enum SpanStatus
{
    /// <summary>
    /// 未设置
    /// </summary>
    Unset,

    /// <summary>
    /// 正常
    /// </summary>
    Ok,

    /// <summary>
    /// 错误
    /// </summary>
    Error,
}

/// <summary>
/// 追踪 Span 接口
/// </summary>
public interface ITraceSpan : IDisposable
{
    /// <summary>
    /// Span ID
    /// </summary>
    string SpanId { get; }

    /// <summary>
    /// 设置属性
    /// </summary>
    void SetAttribute(string key, object value);

    /// <summary>
    /// 设置状态
    /// </summary>
    void SetStatus(SpanStatus status, string? description = null);

    /// <summary>
    /// 记录异常
    /// </summary>
    void RecordException(Exception ex);

    /// <summary>
    /// 启动子 Span
    /// </summary>
    ITraceSpan StartChildSpan(string name);
}
