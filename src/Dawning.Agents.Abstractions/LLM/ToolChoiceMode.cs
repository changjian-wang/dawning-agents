namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Controls how the LLM selects tool calls.
/// </summary>
public enum ToolChoiceMode
{
    /// <summary>The LLM decides whether to call a tool (default).</summary>
    Auto,

    /// <summary>Disable all tool calls.</summary>
    None,

    /// <summary>Force at least one tool call.</summary>
    Required,
}
