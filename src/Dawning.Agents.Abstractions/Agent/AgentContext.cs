namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 执行上下文，包含当前会话的所有状态信息
/// </summary>
public class AgentContext
{
    private readonly List<AgentStep> _steps = [];
    private readonly Dictionary<string, object> _metadata = [];

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 用户原始输入
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// 执行步骤历史（只读视图）
    /// </summary>
    public IReadOnlyList<AgentStep> Steps => _steps;

    /// <summary>
    /// 最大执行步骤数，防止无限循环
    /// </summary>
    public int MaxSteps { get; init; } = 10;

    /// <summary>
    /// 自定义元数据（只读视图）
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>
    /// 添加执行步骤
    /// </summary>
    /// <param name="step">要添加的步骤</param>
    public void AddStep(AgentStep step) => _steps.Add(step);

    /// <summary>
    /// 设置元数据
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void SetMetadata(string key, object value) => _metadata[key] = value;
}
