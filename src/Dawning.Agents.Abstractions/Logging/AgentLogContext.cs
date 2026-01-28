namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// Agent 日志上下文 - 用于在日志中传递 Agent 相关信息
/// </summary>
public class AgentLogContext
{
    private static readonly AsyncLocal<AgentLogContext?> _current = new();

    /// <summary>
    /// 当前日志上下文
    /// </summary>
    public static AgentLogContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// 请求 ID
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 当前工具名称
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// 当前步骤编号
    /// </summary>
    public int? StepNumber { get; set; }

    /// <summary>
    /// 创建作用域
    /// </summary>
    public static IDisposable BeginScope(
        string? agentName = null,
        string? requestId = null,
        string? sessionId = null,
        string? userId = null
    )
    {
        var previous = Current;
        Current = new AgentLogContext
        {
            AgentName = agentName ?? previous?.AgentName,
            RequestId = requestId ?? previous?.RequestId ?? Guid.NewGuid().ToString("N")[..8],
            SessionId = sessionId ?? previous?.SessionId,
            UserId = userId ?? previous?.UserId,
        };
        return new LogContextScope(previous);
    }

    /// <summary>
    /// 设置当前工具
    /// </summary>
    public static void SetTool(string toolName)
    {
        if (Current != null)
        {
            Current.ToolName = toolName;
        }
    }

    /// <summary>
    /// 设置当前步骤
    /// </summary>
    public static void SetStep(int stepNumber)
    {
        if (Current != null)
        {
            Current.StepNumber = stepNumber;
        }
    }

    private sealed class LogContextScope : IDisposable
    {
        private readonly AgentLogContext? _previous;

        public LogContextScope(AgentLogContext? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            Current = _previous;
        }
    }
}
