using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Telemetry;

namespace Dawning.Agents.Core.Telemetry;

/// <summary>
/// Token-tracking LLM provider decorator.
/// </summary>
/// <remarks>
/// Wraps any <see cref="ILLMProvider"/> to automatically record token usage for each call.
///
/// Usage example:
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
    /// Creates a token-tracking LLM provider.
    /// </summary>
    /// <param name="innerProvider">The wrapped LLM provider.</param>
    /// <param name="tracker">The token usage tracker.</param>
    /// <param name="source">Source identifier to distinguish different callers.</param>
    /// <param name="sessionId">Optional session ID.</param>
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
    /// Gets the inner LLM provider.
    /// </summary>
    public ILLMProvider InnerProvider => _innerProvider;

    /// <summary>
    /// Gets the token usage tracker.
    /// </summary>
    public ITokenUsageTracker Tracker => _tracker;

    /// <summary>
    /// Gets the source identifier.
    /// </summary>
    public string Source => _source;

    /// <summary>
    /// Gets the session ID.
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

        // Record token usage
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
        // Streaming responses typically don't return token statistics, but we still forward the request
        // If estimation is needed, it can be calculated manually after the stream ends
        await foreach (
            var chunk in _innerProvider
                .ChatStreamAsync(messages, options, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            yield return chunk;
        }

        // Note: streaming APIs typically don't return token statistics
        // No recording here, or estimation logic can be implemented
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

        // If the last event contains token usage, record it
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
    /// Creates a new decorator instance with a different source identifier.
    /// </summary>
    /// <param name="source">The new source identifier.</param>
    /// <returns>A new decorator instance.</returns>
    public TokenTrackingLLMProvider WithSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return new TokenTrackingLLMProvider(_innerProvider, _tracker, source, _sessionId);
    }

    /// <summary>
    /// Creates a new decorator instance with a different session ID.
    /// </summary>
    /// <param name="sessionId">The new session ID.</param>
    /// <returns>A new decorator instance.</returns>
    public TokenTrackingLLMProvider WithSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return new TokenTrackingLLMProvider(_innerProvider, _tracker, _source, sessionId);
    }
}
