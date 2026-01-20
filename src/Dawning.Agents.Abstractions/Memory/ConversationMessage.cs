namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// 表示对话历史中的消息
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// 消息的唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 角色："user"、"assistant" 或 "system"
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 可选的元数据（例如工具调用、token 数量）
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 此消息的估计 token 数量
    /// </summary>
    public int? TokenCount { get; init; }
}
