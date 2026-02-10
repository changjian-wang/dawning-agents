namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 表示对话中的一条消息
/// </summary>
/// <param name="Role">消息角色（user, assistant, system, tool）</param>
/// <param name="Content">消息内容</param>
public record ChatMessage(string Role, string Content)
{
    /// <summary>
    /// 发送者名称（可选，用于多 Agent 场景区分消息来源）
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// LLM 返回的工具调用列表（仅 assistant 角色）
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }

    /// <summary>
    /// 工具调用 ID（仅 tool 角色，用于关联 assistant 的 ToolCall）
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// 是否包含工具调用
    /// </summary>
    public bool HasToolCalls => ToolCalls is { Count: > 0 };

    /// <summary>创建用户消息</summary>
    public static ChatMessage User(string content) => new("user", content);

    /// <summary>创建助手消息</summary>
    public static ChatMessage Assistant(string content) => new("assistant", content);

    /// <summary>创建系统消息</summary>
    public static ChatMessage System(string content) => new("system", content);

    /// <summary>创建包含工具调用的助手消息</summary>
    public static ChatMessage AssistantWithToolCalls(
        IReadOnlyList<ToolCall> toolCalls,
        string content = ""
    ) => new("assistant", content) { ToolCalls = toolCalls };

    /// <summary>创建工具结果消息</summary>
    public static ChatMessage ToolResult(string toolCallId, string content) =>
        new("tool", content) { ToolCallId = toolCallId };
}
