namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Agent": {
///     "Name": "MyAgent",
///     "Instructions": "You are a helpful assistant.",
///     "MaxSteps": 10
///   }
/// }
/// </code>
/// </remarks>
public class AgentOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Agent 名称
    /// </summary>
    public string Name { get; set; } = "Agent";

    /// <summary>
    /// Agent 系统指令
    /// </summary>
    public string Instructions { get; set; } = "You are a helpful AI assistant.";

    /// <summary>
    /// 最大执行步骤数
    /// </summary>
    public int MaxSteps { get; set; } = 10;

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Agent Name is required");
        }

        if (MaxSteps <= 0)
        {
            throw new InvalidOperationException("MaxSteps must be greater than 0");
        }
    }
}
