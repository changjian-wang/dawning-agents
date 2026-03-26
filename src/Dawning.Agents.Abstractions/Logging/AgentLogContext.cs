namespace Dawning.Agents.Abstractions.Logging;

/// <summary>
/// Provides agent-related context information for structured logging.
/// </summary>
public class AgentLogContext
{
    private static readonly AsyncLocal<AgentLogContext?> s_current = new();

    /// <summary>
    /// Gets or sets the current log context.
    /// </summary>
    public static AgentLogContext? Current
    {
        get => s_current.Value;
        set => s_current.Value = value;
    }

    /// <summary>
    /// The agent name.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// The request ID.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// The session ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// The user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The current tool name.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// The current step number.
    /// </summary>
    public int? StepNumber { get; set; }

    /// <summary>
    /// Begins a new log context scope.
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
    /// Sets the current tool name.
    /// </summary>
    public static void SetTool(string toolName)
    {
        if (Current != null)
        {
            Current.ToolName = toolName;
        }
    }

    /// <summary>
    /// Sets the current step number.
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
