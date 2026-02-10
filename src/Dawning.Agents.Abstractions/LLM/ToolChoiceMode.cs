namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 控制 LLM 如何选择工具调用
/// </summary>
public enum ToolChoiceMode
{
    /// <summary>LLM 自行决定是否调用工具（默认）</summary>
    Auto,

    /// <summary>禁止调用任何工具</summary>
    None,

    /// <summary>强制至少调用一个工具</summary>
    Required,
}
