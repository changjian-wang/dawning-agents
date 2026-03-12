using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// 带 Token 追踪功能的 LLM Provider 装饰器
/// </summary>
/// <remarks>
/// 包装任意 ILLMProvider，自动记录每次调用的 Token 使用情况。
///
/// 使用示例:
/// <code>
/// var tracker = new InMemoryTokenUsageTracker();
/// var trackingProvider = new TokenTrackingLLMProvider(originalProvider, tracker, "MyAgent");
/// </code>
/// </remarks>
public sealed class TokenTrackingLLMProvider : ILLMProvider
{
    private readonly ILLMProvider _innerProvider;
    private readonly ITokenUsageTracker _tracker;
    private readonly string _source;
    private readonly string? _sessionId;

    /// <summary>
    /// 创建带 Token 追踪功能的 LLM Provider
    /// </summary>
    /// <param name="innerProvider">被包装的 LLM Provider</param>
    /// <param name="tracker">Token 使用追踪器</param>
    /// <param name="source">来源标识（用于区分不同的调用者）</param>
    /// <param name="sessionId">会话 ID（可选）</param>
    public TokenTrackingLLMProvider(
        ILLMProvider innerProvider,
        ITokenUsageTracker tracker,
        string source = "Default",
        string? sessionId = null
    )
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _source = source;
        _sessionId = sessionId;
    }

    /// <inheritdoc />
    public string Name => _innerProvider.Name;

    /// <summary>
    /// 获取内部的 LLM Provider
    /// </summary>
    public ILLMProvider InnerProvider => _innerProvider;

    /// <summary>
    /// 获取 Token 追踪器
    /// </summary>
    public ITokenUsageTracker Tracker => _tracker;

    /// <summary>
    /// 来源标识
    /// </summary>
    public string Source => _source;

    /// <summary>
    /// 会话 ID
    /// </summary>
    public string? SessionId => _sessionId;

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var response = await _innerProvider
            .ChatAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        // 记录 Token 使用
        _tracker.Record(
            TokenUsageRecord.Create(
                _source,
                response.PromptTokens,
                response.CompletionTokens,
                _innerProvider.Name,
                _sessionId
            )
        );

        return response;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // 流式响应通常不返回 Token 统计，但我们仍然转发请求
        // 如果需要估算，可以在流结束后手动计算
        await foreach (
            var chunk in _innerProvider
                .ChatStreamAsync(messages, options, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            yield return chunk;
        }

        // 注意：流式 API 通常不返回 token 统计
        // 这里不记录，或者可以实现估算逻辑
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        StreamingTokenUsage? lastUsage = null;

        await foreach (
            var evt in _innerProvider
                .ChatStreamEventsAsync(messages, options, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            if (evt.Usage is not null)
            {
                lastUsage = evt.Usage;
            }

            yield return evt;
        }

        // 如果最后一个事件包含 token 用量，记录它
        if (lastUsage is not null)
        {
            _tracker.Record(
                TokenUsageRecord.Create(
                    _source,
                    lastUsage.PromptTokens,
                    lastUsage.CompletionTokens,
                    _innerProvider.Name,
                    _sessionId
                )
            );
        }
    }

    /// <summary>
    /// 创建一个新的带不同来源标识的装饰器实例
    /// </summary>
    /// <param name="source">新的来源标识</param>
    /// <returns>新的装饰器实例</returns>
    public TokenTrackingLLMProvider WithSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return new TokenTrackingLLMProvider(_innerProvider, _tracker, source, _sessionId);
    }

    /// <summary>
    /// 创建一个新的带不同会话 ID 的装饰器实例
    /// </summary>
    /// <param name="sessionId">新的会话 ID</param>
    /// <returns>新的装饰器实例</returns>
    public TokenTrackingLLMProvider WithSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return new TokenTrackingLLMProvider(_innerProvider, _tracker, _source, sessionId);
    }
}
