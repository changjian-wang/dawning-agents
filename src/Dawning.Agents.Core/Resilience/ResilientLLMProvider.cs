using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// 带弹性策略的 LLM Provider 装饰器
/// </summary>
/// <remarks>
/// 包装 ILLMProvider，为所有调用添加重试、断路器、超时等弹性策略。
/// </remarks>
public class ResilientLLMProvider : ILLMProvider
{
    private readonly ILLMProvider _innerProvider;
    private readonly IResilienceProvider _resilienceProvider;
    private readonly ILogger<ResilientLLMProvider> _logger;

    public string Name => $"Resilient({_innerProvider.Name})";

    public ResilientLLMProvider(
        ILLMProvider innerProvider,
        IResilienceProvider resilienceProvider,
        ILogger<ResilientLLMProvider>? logger = null
    )
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
        _resilienceProvider =
            resilienceProvider ?? throw new ArgumentNullException(nameof(resilienceProvider));
        _logger = logger ?? NullLogger<ResilientLLMProvider>.Instance;

        _logger.LogDebug("ResilientLLMProvider 包装 {InnerProvider}", innerProvider.Name);
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("通过弹性策略执行 ChatAsync");

        return await _resilienceProvider
            .ExecuteAsync(
                async ct =>
                    await _innerProvider.ChatAsync(messages, options, ct).ConfigureAwait(false),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("通过弹性策略执行 ChatStreamAsync");

        // 流式响应特殊处理：在弹性策略内探测首个元素以检测连接/认证错误，
        // 一旦流开始，后续元素不再受弹性策略保护（无法重试部分流）
        IAsyncEnumerator<string>? enumerator = null;
        string? firstElement = null;
        var hasFirst = false;

        try
        {
            await _resilienceProvider
                .ExecuteAsync(
                    async ct =>
                    {
                        // 重试时释放前一次的枚举器
                        if (enumerator is not null)
                        {
                            await enumerator.DisposeAsync().ConfigureAwait(false);
                            enumerator = null;
                        }

                        var stream = _innerProvider.ChatStreamAsync(
                            messages,
                            options,
                            cancellationToken
                        );
                        enumerator = stream.GetAsyncEnumerator(cancellationToken);

                        // 探测首个元素：MoveNextAsync 触发实际 I/O（HTTP 连接、认证等），
                        // 通过 WaitAsync(ct) 让 Polly 超时策略可以中断探测
                        hasFirst = await enumerator
                            .MoveNextAsync()
                            .AsTask()
                            .WaitAsync(ct)
                            .ConfigureAwait(false);
                        if (hasFirst)
                        {
                            firstElement = enumerator.Current;
                        }
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (enumerator is null)
            {
                yield break;
            }

            // 输出已探测的首个元素
            if (hasFirst)
            {
                yield return firstElement!;
            }

            // 继续迭代剩余元素
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("通过弹性策略执行 ChatStreamEventsAsync");

        IAsyncEnumerator<StreamingChatEvent>? enumerator = null;
        StreamingChatEvent? firstElement = null;
        var hasFirst = false;

        try
        {
            await _resilienceProvider
                .ExecuteAsync(
                    async ct =>
                    {
                        if (enumerator is not null)
                        {
                            await enumerator.DisposeAsync().ConfigureAwait(false);
                            enumerator = null;
                        }

                        var stream = _innerProvider.ChatStreamEventsAsync(
                            messages,
                            options,
                            cancellationToken
                        );
                        enumerator = stream.GetAsyncEnumerator(cancellationToken);

                        hasFirst = await enumerator
                            .MoveNextAsync()
                            .AsTask()
                            .WaitAsync(ct)
                            .ConfigureAwait(false);
                        if (hasFirst)
                        {
                            firstElement = enumerator.Current;
                        }
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (enumerator is null)
            {
                yield break;
            }

            if (hasFirst)
            {
                yield return firstElement!;
            }

            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
