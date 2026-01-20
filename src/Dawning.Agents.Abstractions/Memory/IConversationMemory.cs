using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// 对话记忆管理接口
/// </summary>
/// <remarks>
/// <para>Memory 系统用于管理 Agent 的对话历史和上下文</para>
/// <para>实现类型包括：BufferMemory、WindowMemory、SummaryMemory</para>
/// </remarks>
public interface IConversationMemory
{
    /// <summary>
    /// 向记忆添加消息
    /// </summary>
    /// <param name="message">要添加的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取记忆中的所有消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取格式化为 LLM 上下文的消息
    /// </summary>
    /// <param name="maxTokens">最大 token 数量限制（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>可直接用于 LLM 调用的消息列表</returns>
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清除记忆中的所有消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前 token 数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前记忆的 token 总数</returns>
    Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取消息数量
    /// </summary>
    int MessageCount { get; }
}
