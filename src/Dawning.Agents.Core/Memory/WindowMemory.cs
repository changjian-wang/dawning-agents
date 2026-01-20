using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 只保留最后 N 条消息的滑动窗口记忆
/// </summary>
/// <remarks>
/// <para>适用于需要控制内存使用的长对话场景</para>
/// <para>当消息数量超过窗口大小时，自动丢弃最旧的消息</para>
/// <para>线程安全</para>
/// </remarks>
public class WindowMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _messages = [];
    private readonly ITokenCounter _tokenCounter;
    private readonly int _windowSize;
    private readonly Lock _lock = new();

    /// <summary>
    /// 获取当前窗口内的消息数量（最大为 WindowSize）
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
    /// 获取窗口大小
    /// </summary>
    public int WindowSize => _windowSize;

    /// <summary>
    /// 初始化滑动窗口记忆
    /// </summary>
    /// <param name="tokenCounter">Token 计数器</param>
    /// <param name="windowSize">窗口大小（保留的最大消息数）</param>
    /// <exception cref="ArgumentException">窗口大小必须为正数</exception>
    public WindowMemory(ITokenCounter tokenCounter, int windowSize = 10)
    {
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _windowSize =
            windowSize > 0
                ? windowSize
                : throw new ArgumentException("窗口大小必须为正数", nameof(windowSize));
    }

    /// <summary>
    /// 向窗口添加消息，超出窗口大小时自动移除最旧的消息
    /// </summary>
    public Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                message = message with { TokenCount = tokenCount };
            }

            _messages.Add(message);

            // 裁剪到窗口大小
            while (_messages.Count > _windowSize)
            {
                _messages.RemoveAt(0);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取窗口内所有消息的副本
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
    /// 获取用于 LLM 调用的消息上下文
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            var result = _messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    /// <summary>
    /// 清空窗口内的所有消息
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
    /// 获取窗口内所有消息的 token 总数
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_messages.Sum(m => m.TokenCount ?? 0));
        }
    }
}
