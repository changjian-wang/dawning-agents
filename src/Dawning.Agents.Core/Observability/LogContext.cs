namespace Dawning.Agents.Core.Observability;

using System.Collections.Concurrent;

/// <summary>
/// Provides contextual information for logging.
/// </summary>
public sealed class LogContext : IDisposable
{
    private static readonly AsyncLocal<LogContext?> s_current = new();
    private readonly ConcurrentDictionary<string, object> _properties = new();
    private readonly LogContext? _parent;

    /// <summary>
    /// Gets the current log context.
    /// </summary>
    public static LogContext? Current => s_current.Value;

    private LogContext(LogContext? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Pushes a new log context scope.
    /// </summary>
    public static LogContext Push()
    {
        var context = new LogContext(s_current.Value);
        s_current.Value = context;
        return context;
    }

    /// <summary>
    /// Sets a context property.
    /// </summary>
    public LogContext Set(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Gets a context property.
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
    /// Gets all properties including those from parent contexts.
    /// </summary>
    public IDictionary<string, object> GetAllProperties()
    {
        var result = new Dictionary<string, object>();

        // Get parent context properties first
        if (_parent != null)
        {
            foreach (var (key, value) in _parent.GetAllProperties())
            {
                result[key] = value;
            }
        }

        // Override with current context
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
    }
}

/// <summary>
/// Extension methods for <see cref="LogContext"/>.
/// </summary>
public static class LogContextExtensions
{
    /// <summary>
    /// Sets the request ID.
    /// </summary>
    public static LogContext WithRequestId(this LogContext context, string requestId) =>
        context.Set("RequestId", requestId);

    /// <summary>
    /// Sets the agent name.
    /// </summary>
    public static LogContext WithAgentName(this LogContext context, string agentName) =>
        context.Set("AgentName", agentName);

    /// <summary>
    /// Sets the user ID.
    /// </summary>
    public static LogContext WithUserId(this LogContext context, string userId) =>
        context.Set("UserId", userId);

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    public static LogContext WithSessionId(this LogContext context, string sessionId) =>
        context.Set("SessionId", sessionId);

    /// <summary>
    /// Sets the correlation ID.
    /// </summary>
    public static LogContext WithCorrelationId(this LogContext context, string correlationId) =>
        context.Set("CorrelationId", correlationId);
}
