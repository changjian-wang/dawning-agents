namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 升级到人工处理的异常
/// </summary>
public class AgentEscalationException : Exception
{
    /// <summary>
    /// 升级原因
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// 详细描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 上下文数据
    /// </summary>
    public IDictionary<string, object> Context { get; }

    /// <summary>
    /// 已尝试的解决方案
    /// </summary>
    public IReadOnlyList<string> AttemptedSolutions { get; }

    /// <summary>
    /// 创建升级异常
    /// </summary>
    public AgentEscalationException(
        string reason,
        string description,
        IDictionary<string, object>? context = null,
        IReadOnlyList<string>? attemptedSolutions = null
    )
        : base(reason)
    {
        Reason = reason;
        Description = description;
        Context = context ?? new Dictionary<string, object>();
        AttemptedSolutions = attemptedSolutions ?? [];
    }

    /// <summary>
    /// 创建升级异常（带内部异常）
    /// </summary>
    public AgentEscalationException(
        string reason,
        string description,
        Exception innerException,
        IDictionary<string, object>? context = null,
        IReadOnlyList<string>? attemptedSolutions = null
    )
        : base(reason, innerException)
    {
        Reason = reason;
        Description = description;
        Context = context ?? new Dictionary<string, object>();
        AttemptedSolutions = attemptedSolutions ?? [];
    }
}
