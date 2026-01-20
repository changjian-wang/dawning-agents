using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 存储所有消息的简单缓冲记忆
/// </summary>
/// <remarks>
/// <para>最简单的记忆实现，将所有消息存储在列表中</para>
/// <para>适用于短对话或测试场景</para>
/// <para>线程安全</para>
/// </remarks>
public class BufferMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly ITokenCounter _tokenCounter;
    private readonly Lock _lock = new();

    /// <summary>
    /// 获取当前存储的消息数量
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }

    /// <summary>
    /// 初始化缓冲记忆
    /// </summary>
    /// <param name="tokenCounter">Token 计数器</param>
    public BufferMemory(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
    }

    /// <summary>
    /// 向缓冲区添加消息，如果未提供 TokenCount 则自动计算
    /// </summary>
    public Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            // 如果未提供则计算 token 数量
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }

            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取缓冲区中的所有消息副本
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(_messages.ToList());
        }
    }

    /// <summary>
    /// 获取用于 LLM 调用的消息上下文，可选择按 token 限制裁剪
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            IEnumerable<ConversationMessage> messages = _messages;

            if (maxTokens.HasValue)
            {
                // 取适合 token 限制的最近消息
                messages = TrimToTokenLimit(_messages, maxTokens.Value);
            }

            var result = messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    /// <summary>
    /// 清空缓冲区中的所有消息
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _messages.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取缓冲区中所有消息的 token 总数
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var total = _messages.Sum(m => m.TokenCount ?? 0);
            return Task.FromResult(total);
        }
    }

    /// <summary>
    /// 裁剪消息以适应 token 限制
    /// </summary>
    private static IEnumerable<ConversationMessage> TrimToTokenLimit(
        List<ConversationMessage> messages,
        int maxTokens
    )
    {
        var result = new List<ConversationMessage>();
        var tokenCount = 0;

        // 从最近的消息开始
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            var msgTokens = messages[i].TokenCount ?? 0;
            if (tokenCount + msgTokens > maxTokens)
            {
                break;
            }

            result.Insert(0, messages[i]);
            tokenCount += msgTokens;
        }

        return result;
    }
}
