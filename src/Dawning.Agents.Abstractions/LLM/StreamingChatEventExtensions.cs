using System.Runtime.CompilerServices;

namespace Dawning.Agents.Abstractions.LLM;

/// <summary>
/// <see cref="StreamingChatEvent"/> 相关扩展方法
/// </summary>
public static class StreamingChatEventExtensions
{
    /// <summary>
    /// 将文本流转换为结构化事件流
    /// </summary>
    /// <remarks>
    /// 将 <c>IAsyncEnumerable&lt;string&gt;</c> 的每个文本片段包装为
    /// <see cref="StreamingChatEvent.ContentDelta"/> 事件。
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
    /// 从结构化事件流中提取纯文本流
    /// </summary>
    /// <remarks>
    /// 向下兼容方法：只提取 <see cref="StreamingChatEvent.ContentDelta"/> 部分，
    /// 忽略 tool call delta、finish reason 和 usage 信息。
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
    /// 从结构化事件流中累积完整的工具调用列表
    /// </summary>
    /// <remarks>
    /// 遍历事件流，将分散的 <see cref="ToolCallDelta"/> 累积拼接为完整的 <see cref="ToolCall"/>。
    /// 同时收集所有 content delta 为完整文本。
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
/// 流式事件累积器，将增量事件组装为完整响应
/// </summary>
public class StreamingAccumulator
{
    private readonly System.Text.StringBuilder _contentBuilder = new();
    private readonly Dictionary<int, ToolCallBuilder> _toolCallBuilders = new();

    /// <summary>累积的完整文本内容</summary>
    public string Content => _contentBuilder.ToString();

    /// <summary>结束原因</summary>
    public string? FinishReason { get; private set; }

    /// <summary>Token 用量</summary>
    public StreamingTokenUsage? Usage { get; private set; }

    /// <summary>累积的完整工具调用列表</summary>
    public IReadOnlyList<ToolCall> ToolCalls =>
        _toolCallBuilders.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value.Build()).ToList();

    /// <summary>是否包含工具调用</summary>
    public bool HasToolCalls => _toolCallBuilders.Count > 0;

    /// <summary>
    /// 添加一个流式事件
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
    /// 转换为 <see cref="ChatCompletionResponse"/>
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
