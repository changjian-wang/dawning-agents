using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 通过摘要旧消息来节省 token 的记忆
/// </summary>
/// <remarks>
/// <para>当消息数量超过阈值时，自动将旧消息摘要为简短内容</para>
/// <para>适用于需要保持长期上下文但又要控制 token 消耗的场景</para>
/// <para>需要 LLM 提供者来生成摘要</para>
/// <para>线程安全</para>
/// </remarks>
public class SummaryMemory : IConversationMemory
{
    private readonly List<ConversationMessage> _recentMessages = [];
    private string _summary = string.Empty;
    private readonly ILLMProvider _llm;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _maxRecentMessages;
    private readonly int _summaryThreshold;
    private readonly Lock _lock = new();
    private readonly ILogger<SummaryMemory> _logger;

    /// <summary>
    /// 获取消息数量（包括摘要系统消息和最近消息）
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _recentMessages.Count + (string.IsNullOrEmpty(_summary) ? 0 : 1);
            }
        }
    }

    /// <summary>
    /// 获取当前摘要内容
    /// </summary>
    public string Summary
    {
        get
        {
            lock (_lock)
            {
                return _summary;
            }
        }
    }

    /// <summary>
    /// 获取最大保留的最近消息数
    /// </summary>
    public int MaxRecentMessages => _maxRecentMessages;

    /// <summary>
    /// 初始化摘要记忆
    /// </summary>
    /// <param name="llm">LLM 提供者，用于生成摘要</param>
    /// <param name="tokenCounter">Token 计数器</param>
    /// <param name="maxRecentMessages">保留的最近消息数（默认 6）</param>
    /// <param name="summaryThreshold">触发摘要的消息数阈值（默认 10）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SummaryMemory(
        ILLMProvider llm,
        ITokenCounter tokenCounter,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        ILogger<SummaryMemory>? logger = null
    )
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _maxRecentMessages =
            maxRecentMessages > 0
                ? maxRecentMessages
                : throw new ArgumentException("最大消息数必须为正数", nameof(maxRecentMessages));
        _summaryThreshold =
            summaryThreshold > maxRecentMessages
                ? summaryThreshold
                : throw new ArgumentException(
                    "摘要阈值必须大于最大保留消息数",
                    nameof(summaryThreshold)
                );
        _logger = logger ?? NullLogger<SummaryMemory>.Instance;
    }

    /// <summary>
    /// 向记忆添加消息，超过阈值时自动触发摘要生成
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ConversationMessage messageWithTokens;
        List<ConversationMessage>? messagesToSummarize = null;

        lock (_lock)
        {
            if (message.TokenCount == null)
            {
                var tokenCount = _tokenCounter.CountTokens(message.Content);
                messageWithTokens = message with { TokenCount = tokenCount };
            }
            else
            {
                messageWithTokens = message;
            }

            _recentMessages.Add(messageWithTokens);

            // 检查是否需要摘要
            if (_recentMessages.Count >= _summaryThreshold)
            {
                // 取最旧的消息进行摘要（保留最近的）
                var toSummarize = _recentMessages.Count - _maxRecentMessages;
                messagesToSummarize = _recentMessages.Take(toSummarize).ToList();
                _recentMessages.RemoveRange(0, toSummarize);

                _logger.LogDebug(
                    "触发摘要，处理 {Count} 条消息，保留 {Remaining} 条最近消息",
                    toSummarize,
                    _recentMessages.Count
                );
            }
        }

        // 在锁外进行摘要（避免长时间持有锁）
        if (messagesToSummarize != null)
        {
            await SummarizeMessagesAsync(messagesToSummarize, cancellationToken);
        }
    }

    /// <summary>
    /// 生成消息摘要
    /// </summary>
    private async Task SummarizeMessagesAsync(
        List<ConversationMessage> messages,
        CancellationToken cancellationToken
    )
    {
        var conversationText = string.Join("\n", messages.Select(m => $"{m.Role}: {m.Content}"));

        string currentSummary;
        lock (_lock)
        {
            currentSummary = _summary;
        }

        var prompt = $"""
            请总结以下对话，保留关键信息和上下文。

            {(string.IsNullOrEmpty(currentSummary) ? "" : $"之前的摘要：\n{currentSummary}\n")}
            新消息：
            {conversationText}

            请提供简洁的摘要，包含：
            1. 讨论的主要话题
            2. 关键决定或结论
            3. 未来对话的重要上下文

            摘要：
            """;

        try
        {
            var response = await _llm.ChatAsync(
                [new ChatMessage("user", prompt)],
                new ChatCompletionOptions { Temperature = 0.3f, MaxTokens = 500 },
                cancellationToken
            );

            lock (_lock)
            {
                _summary = response.Content ?? string.Empty;
            }

            _logger.LogDebug("摘要生成成功，长度: {Length} 字符", _summary.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成摘要失败，将保留原始消息");
            // 摘要失败时，将消息放回
            lock (_lock)
            {
                _recentMessages.InsertRange(0, messages);
            }
        }
    }

    /// <summary>
    /// 获取所有消息（包括摘要和最近消息）
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        lock (_lock)
        {
            var result = new List<ConversationMessage>();

            // 如果存在摘要，作为系统消息添加
            if (!string.IsNullOrEmpty(_summary))
            {
                result.Add(
                    new ConversationMessage
                    {
                        Role = "system",
                        Content = $"之前对话的摘要：{_summary}",
                        TokenCount = _tokenCounter.CountTokens(_summary),
                    }
                );
            }

            result.AddRange(_recentMessages);
            return Task.FromResult<IReadOnlyList<ConversationMessage>>(result);
        }
    }

    /// <summary>
    /// 获取用于 LLM 调用的消息上下文（包括摘要和最近消息）
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        var messages = await GetMessagesAsync(cancellationToken);
        return messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();
    }

    /// <summary>
    /// 清空所有消息和摘要
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _recentMessages.Clear();
            _summary = string.Empty;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取总 token 数（包括摘要和最近消息）
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var recentTokens = _recentMessages.Sum(m => m.TokenCount ?? 0);
            var summaryTokens = string.IsNullOrEmpty(_summary)
                ? 0
                : _tokenCounter.CountTokens(_summary);
            return Task.FromResult(recentTokens + summaryTokens);
        }
    }
}
