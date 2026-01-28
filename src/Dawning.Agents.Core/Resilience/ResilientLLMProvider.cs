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
        _innerProvider =
            innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
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

        return await _resilienceProvider.ExecuteAsync(
            async ct => await _innerProvider.ChatAsync(messages, options, ct),
            cancellationToken
        );
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("通过弹性策略执行 ChatStreamAsync");

        // 流式响应特殊处理：在开始流之前应用弹性策略
        // 一旦流开始，我们不能重试整个流
        IAsyncEnumerator<string>? enumerator = null;

        try
        {
            // 初始化流（可能失败）
            await _resilienceProvider.ExecuteAsync(
                async ct =>
                {
                    var stream = _innerProvider.ChatStreamAsync(messages, options, ct);
                    enumerator = stream.GetAsyncEnumerator(ct);
                },
                cancellationToken
            );

            if (enumerator is null)
            {
                yield break;
            }

            // 流式返回内容
            while (await enumerator.MoveNextAsync())
            {
                yield return enumerator.Current;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }
}
