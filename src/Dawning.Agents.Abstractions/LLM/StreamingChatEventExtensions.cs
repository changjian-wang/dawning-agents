using System.Runtime.CompilerServices;

namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// Extension methods for <see cref="StreamingChatEvent"/>.
/// </summary>
public static class StreamingChatEventExtensions
{
    /// <summary>
    /// Converts a text stream to a structured event stream.
    /// </summary>
    /// <remarks>
    /// Wraps each text fragment from <c>IAsyncEnumerable&lt;string&gt;</c> as a
    /// <see cref="StreamingChatEvent.ContentDelta"/> event.
    /// </remarks>
    public static async IAsyncEnumerable<StreamingChatEvent> ToStreamingEvents(
        this IAsyncEnumerable<string> textStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var text in textStream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            if (!string.IsNullOrEmpty(text))
            {
                yield return StreamingChatEvent.Content(text);
            }
        }

        yield return StreamingChatEvent.Done("stop");
    }

    /// <summary>
    /// Extracts a plain text stream from a structured event stream.
    /// </summary>
    /// <remarks>
    /// Backward-compatible method: extracts only <see cref="StreamingChatEvent.ContentDelta"/> parts,
    /// ignoring tool call deltas, finish reasons, and usage information.
    /// </remarks>
    public static async IAsyncEnumerable<string> AsTextStream(
        this IAsyncEnumerable<StreamingChatEvent> eventStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var evt in eventStream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            if (!string.IsNullOrEmpty(evt.ContentDelta))
            {
                yield return evt.ContentDelta;
            }
        }
    }

    /// <summary>
    /// Accumulates complete tool calls from a structured event stream.
    /// </summary>
    /// <remarks>
    /// Iterates through the event stream, accumulating scattered <see cref="ToolCallDelta"/> fragments
    /// into complete <see cref="ToolCall"/> objects. Also collects all content deltas into the full text.
    /// </remarks>
    public static async Task<StreamingAccumulator> AccumulateAsync(
        this IAsyncEnumerable<StreamingChatEvent> eventStream,
        CancellationToken cancellationToken = default
    )
    {
        var accumulator = new StreamingAccumulator();

        await foreach (
            var evt in eventStream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            accumulator.Add(evt);
        }

        return accumulator;
    }
}

/// <summary>
/// Streaming event accumulator that assembles incremental events into a complete response.
/// </summary>
public class StreamingAccumulator
{
    private readonly System.Text.StringBuilder _contentBuilder = new();
    private readonly Dictionary<int, ToolCallBuilder> _toolCallBuilders = new();

    /// <summary>Accumulated complete text content.</summary>
    public string Content => _contentBuilder.ToString();

    /// <summary>Finish reason.</summary>
    public string? FinishReason { get; private set; }

    /// <summary>Token usage.</summary>
    public StreamingTokenUsage? Usage { get; private set; }

    /// <summary>Accumulated complete tool call list.</summary>
    public IReadOnlyList<ToolCall> ToolCalls =>
        _toolCallBuilders.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value.Build()).ToList();

    /// <summary>Whether there are tool calls.</summary>
    public bool HasToolCalls => _toolCallBuilders.Count > 0;

    /// <summary>
    /// Adds a streaming event.
    /// </summary>
    public void Add(StreamingChatEvent evt)
    {
        if (!string.IsNullOrEmpty(evt.ContentDelta))
        {
            _contentBuilder.Append(evt.ContentDelta);
        }

        if (evt.ToolCallDelta is { } delta)
        {
            if (!_toolCallBuilders.TryGetValue(delta.Index, out var builder))
            {
                builder = new ToolCallBuilder();
                _toolCallBuilders[delta.Index] = builder;
            }

            builder.Apply(delta);
        }

        if (evt.FinishReason is not null)
        {
            FinishReason = evt.FinishReason;
        }

        if (evt.Usage is not null)
        {
            Usage = evt.Usage;
        }
    }

    /// <summary>
    /// Converts to a <see cref="ChatCompletionResponse"/>.
    /// </summary>
    public ChatCompletionResponse ToChatCompletionResponse()
    {
        var toolCalls = HasToolCalls ? ToolCalls : null;
        return new ChatCompletionResponse
        {
            Content = Content,
            FinishReason = FinishReason,
            ToolCalls = toolCalls,
            PromptTokens = Usage?.PromptTokens ?? 0,
            CompletionTokens = Usage?.CompletionTokens ?? 0,
        };
    }

    private class ToolCallBuilder
    {
        private string? _id;
        private string? _functionName;
        private readonly System.Text.StringBuilder _arguments = new();

        public void Apply(ToolCallDelta delta)
        {
            if (delta.Id is not null)
            {
                _id = delta.Id;
            }

            if (delta.FunctionName is not null)
            {
                _functionName = delta.FunctionName;
            }

            if (delta.ArgumentsDelta is not null)
            {
                _arguments.Append(delta.ArgumentsDelta);
            }
        }

        public ToolCall Build() =>
            new(
                _id ?? $"call_{Guid.NewGuid():N}",
                _functionName ?? string.Empty,
                _arguments.ToString()
            );
    }
}
