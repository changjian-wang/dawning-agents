namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// 表示对话中的一条消息
/// </summary>
/// <param name="Role">消息角色（user, assistant, system）</param>
/// <param name="Content">消息内容</param>
public record ChatMessage(string Role, string Content);
