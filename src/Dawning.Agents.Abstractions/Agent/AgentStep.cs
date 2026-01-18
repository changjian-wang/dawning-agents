namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 单步执行记录
/// </summary>
public record AgentStep
{
    /// <summary>
    /// 步骤序号
    /// </summary>
    public required int StepNumber { get; init; }

    /// <summary>
    /// Agent 的思考过程
    /// </summary>
    public string? Thought { get; init; }

    /// <summary>
    /// 执行的动作名称
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// 动作的输入参数
    /// </summary>
    public string? ActionInput { get; init; }

    /// <summary>
    /// 动作执行后的观察结果
    /// </summary>
    public string? Observation { get; init; }

    /// <summary>
    /// 步骤执行时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
