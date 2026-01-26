namespace Dawning.Agents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// 为日志提供上下文信息
/// </summary>
public sealed class LogContext : IDisposable
{
    private static readonly AsyncLocal<LogContext?> s_current = new();
    private readonly ConcurrentDictionary<string, object> _properties = new();
    private readonly LogContext? _parent;

    /// <summary>
    /// 当前日志上下文
    /// </summary>
    public static LogContext? Current => s_current.Value;

    private LogContext(LogContext? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// 推入新的日志上下文作用域
    /// </summary>
    public static LogContext Push()
    {
        var context = new LogContext(s_current.Value);
        s_current.Value = context;
        return context;
    }

    /// <summary>
    /// 设置上下文属性
    /// </summary>
    public LogContext Set(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// 获取上下文属性
    /// </summary>
    public object? Get(string key)
    {
        if (_properties.TryGetValue(key, out var value))
        {
            return value;
        }

        return _parent?.Get(key);
    }

    /// <summary>
    /// 获取包括父上下文的所有属性
    /// </summary>
    public IDictionary<string, object> GetAllProperties()
    {
        var result = new Dictionary<string, object>();

        // 首先获取父上下文属性
        if (_parent != null)
        {
            foreach (var (key, value) in _parent.GetAllProperties())
            {
                result[key] = value;
            }
        }

        // 用当前上下文覆盖
        foreach (var (key, value) in _properties)
        {
            result[key] = value;
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        s_current.Value = _parent;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 日志上下文扩展
/// </summary>
public static class LogContextExtensions
{
    /// <summary>
    /// 设置请求 ID
    /// </summary>
    public static LogContext WithRequestId(this LogContext context, string requestId) =>
        context.Set("RequestId", requestId);

    /// <summary>
    /// 设置 Agent 名称
    /// </summary>
    public static LogContext WithAgentName(this LogContext context, string agentName) =>
        context.Set("AgentName", agentName);

    /// <summary>
    /// 设置用户 ID
    /// </summary>
    public static LogContext WithUserId(this LogContext context, string userId) =>
        context.Set("UserId", userId);

    /// <summary>
    /// 设置会话 ID
    /// </summary>
    public static LogContext WithSessionId(this LogContext context, string sessionId) =>
        context.Set("SessionId", sessionId);

    /// <summary>
    /// 设置关联 ID
    /// </summary>
    public static LogContext WithCorrelationId(this LogContext context, string correlationId) =>
        context.Set("CorrelationId", correlationId);
}
