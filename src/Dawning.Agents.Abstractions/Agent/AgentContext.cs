namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 执行上下文，包含当前会话的所有状态信息
/// </summary>
public class AgentContext
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 用户原始输入
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// 执行步骤历史
    /// </summary>
    public List<AgentStep> Steps { get; } = [];

    /// <summary>
    /// 最大执行步骤数，防止无限循环
    /// </summary>
    public int MaxSteps { get; init; } = 10;

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}
