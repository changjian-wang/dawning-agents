using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// 带路由功能的 LLM Provider
/// </summary>
/// <remarks>
/// 包装 IModelRouter，提供 ILLMProvider 接口，支持：
/// <list type="bullet">
///   <item>自动路由选择</item>
///   <item>故障转移</item>
///   <item>调用统计</item>
/// </list>
/// </remarks>
public class RoutingLLMProvider : ILLMProvider
{
    private readonly IModelRouter _router;
    private readonly ModelRouterOptions _options;
    private readonly ILogger<RoutingLLMProvider> _logger;
    private readonly ITokenCounter? _tokenCounter;

    public string Name => $"Routing({_router.Name})";

    public RoutingLLMProvider(
        IModelRouter router,
        IOptions<ModelRouterOptions> options,
        ILogger<RoutingLLMProvider>? logger = null,
        ITokenCounter? tokenCounter = null
    )
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _options = options?.Value ?? new ModelRouterOptions();
        _logger = logger ?? NullLogger<RoutingLLMProvider>.Instance;
        _tokenCounter = tokenCounter;
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var messageList = messages.ToList();
        var context = CreateRoutingContext(messageList, options);
        var excludedProviders = new List<string>();
        var maxRetries = _options.EnableFailover ? _options.MaxFailoverRetries : 0;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            context = context with { ExcludedProviders = excludedProviders };

            ILLMProvider provider;
            try
            {
                provider = await _router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("所有提供者都被排除，重置排除列表重试");
                excludedProviders.Clear();
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogDebug(
                    "尝试 {Attempt}/{MaxRetries} 使用提供者: {Provider}",
                    attempt + 1,
                    maxRetries + 1,
                    provider.Name
                );

                var response = await provider
                    .ChatAsync(messageList, options, cancellationToken)
                    .ConfigureAwait(false);
                sw.Stop();

                // 报告成功
                var cost = CalculateCost(
                    provider.Name,
                    response.PromptTokens,
                    response.CompletionTokens
                );
                _router.ReportResult(
                    provider,
                    ModelCallResult.Succeeded(
                        sw.ElapsedMilliseconds,
                        response.PromptTokens,
                        response.CompletionTokens,
                        cost
                    )
                );

                return response;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException
                    && attempt < maxRetries
                    && _options.EnableFailover
                )
            {
                sw.Stop();
                _logger.LogWarning(ex, "提供者 {Provider} 调用失败，尝试故障转移", provider.Name);

                // 报告失败
                _router.ReportResult(
                    provider,
                    ModelCallResult.Failed(ex.Message, sw.ElapsedMilliseconds)
                );
                excludedProviders.Add(provider.Name);
            }
        }

        throw new InvalidOperationException("All providers failed after failover attempts");
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var messageList = messages.ToList();
        var context = CreateRoutingContext(messageList, options);
        var excludedProviders = new List<string>();
        var maxRetries = _options.EnableFailover ? _options.MaxFailoverRetries : 0;

        Exception? lastException = null;
        IAsyncEnumerable<string>? successfulStream = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            context = context with { ExcludedProviders = excludedProviders };

            ILLMProvider provider;
            try
            {
                provider = await _router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("所有提供者都被排除，重置排除列表重试");
                excludedProviders.Clear();
                continue;
            }

            _logger.LogDebug(
                "流式尝试 {Attempt}/{MaxRetries} 使用提供者: {Provider}",
                attempt + 1,
                maxRetries + 1,
                provider.Name
            );

            // 尝试获取流
            var (stream, error) = await TryGetStreamAsync(
                    provider,
                    messageList,
                    options,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (stream != null)
            {
                successfulStream = WrapStreamWithReporting(stream, provider, messageList);
                break;
            }

            if (error != null)
            {
                _logger.LogWarning(
                    error,
                    "流式提供者 {Provider} 初始化失败，尝试故障转移",
                    provider.Name
                );
                _router.ReportResult(provider, ModelCallResult.Failed(error.Message, 0));
                excludedProviders.Add(provider.Name);
                lastException = error;
            }
        }

        if (successfulStream == null)
        {
            throw new InvalidOperationException(
                "All providers failed after failover attempts",
                lastException
            );
        }

        await foreach (
            var chunk in successfulStream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var messageList = messages.ToList();
        var context = CreateRoutingContext(messageList, options);
        var excludedProviders = new List<string>();
        var maxRetries = _options.EnableFailover ? _options.MaxFailoverRetries : 0;

        Exception? lastException = null;
        IAsyncEnumerable<StreamingChatEvent>? successfulStream = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            context = context with { ExcludedProviders = excludedProviders };

            ILLMProvider provider;
            try
            {
                provider = await _router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("所有提供者都被排除，重置排除列表重试");
                excludedProviders.Clear();
                continue;
            }

            _logger.LogDebug(
                "事件流尝试 {Attempt}/{MaxRetries} 使用提供者: {Provider}",
                attempt + 1,
                maxRetries + 1,
                provider.Name
            );

            // 异步迭代器的 try/catch 无法捕获延迟执行的错误，
            // 必须通过 TryGetEventStreamAsync 探测首个元素以检测实际错误
            var (stream, error) = await TryGetEventStreamAsync(
                    provider,
                    messageList,
                    options,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (stream != null)
            {
                successfulStream = WrapEventStreamWithReporting(stream, provider, messageList);
                break;
            }

            if (error != null)
            {
                _logger.LogWarning(
                    error,
                    "事件流提供者 {Provider} 初始化失败，尝试故障转移",
                    provider.Name
                );
                _router.ReportResult(provider, ModelCallResult.Failed(error.Message, 0));
                excludedProviders.Add(provider.Name);
                lastException = error;
            }
        }

        if (successfulStream == null)
        {
            throw new InvalidOperationException(
                "All providers failed after failover attempts",
                lastException
            );
        }

        await foreach (
            var evt in successfulStream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            yield return evt;
        }
    }

    private async Task<(IAsyncEnumerable<string>? Stream, Exception? Error)> TryGetStreamAsync(
        ILLMProvider provider,
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var stream = provider.ChatStreamAsync(messages, options, cancellationToken);
            // 异步迭代器方法在调用时不执行任何代码，错误延迟到首次 MoveNextAsync，
            // 因此必须通过 ProbeStreamAsync 探测首个元素以检测连接/认证错误
            return await ProbeStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex);
        }
    }

    private async Task<(
        IAsyncEnumerable<StreamingChatEvent>? Stream,
        Exception? Error
    )> TryGetEventStreamAsync(
        ILLMProvider provider,
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions? options,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var stream = provider.ChatStreamEventsAsync(messages, options, cancellationToken);
            return await ProbeStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex);
        }
    }

    /// <summary>
    /// 探测异步流的首个元素，用于在迭代前检测提供者连接/认证错误。
    /// 成功时返回包含首个元素 + 剩余流的组合流；失败时返回异常。
    /// </summary>
    private static async Task<(IAsyncEnumerable<T>? Stream, Exception? Error)> ProbeStreamAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken cancellationToken
    )
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        var ownershipTransferred = false;
        try
        {
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                return (EmptyAsyncEnumerable<T>(), null);
            }

            ownershipTransferred = true;
            return (PrependAsync(enumerator.Current, enumerator), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 将已获取的首个元素与剩余枚举器合并为单一异步流。
    /// 调用方通过 ProbeStreamAsync 获取 first 后，所有权转移给本方法负责 Dispose。
    /// </summary>
    private static async IAsyncEnumerable<T> PrependAsync<T>(T first, IAsyncEnumerator<T> remaining)
    {
        try
        {
            yield return first;
            while (await remaining.MoveNextAsync().ConfigureAwait(false))
            {
                yield return remaining.Current;
            }
        }
        finally
        {
            await remaining.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        yield break;
    }

    private async IAsyncEnumerable<string> WrapStreamWithReporting(
        IAsyncEnumerable<string> stream,
        ILLMProvider provider,
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var totalChars = 0;

        await foreach (
            var chunk in stream.WithCancellation(cancellationToken).ConfigureAwait(false)
        )
        {
            totalChars += chunk.Length;
            yield return chunk;
        }

        sw.Stop();

        // 估算输出 token 数（流式响应无法获取精确 token 计数）
        var outputTokens = totalChars / 4;
        var inputTokens = EstimateInputTokens(messages);
        var cost = CalculateCost(provider.Name, inputTokens, outputTokens);
        _router.ReportResult(
            provider,
            ModelCallResult.Succeeded(sw.ElapsedMilliseconds, inputTokens, outputTokens, cost)
        );
    }

    private async IAsyncEnumerable<StreamingChatEvent> WrapEventStreamWithReporting(
        IAsyncEnumerable<StreamingChatEvent> stream,
        ILLMProvider provider,
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var totalChars = 0;

        await foreach (var evt in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            totalChars += evt.ContentDelta?.Length ?? 0;
            yield return evt;
        }

        sw.Stop();

        var outputTokens = totalChars / 4;
        var inputTokens = EstimateInputTokens(messages);
        var cost = CalculateCost(provider.Name, inputTokens, outputTokens);
        _router.ReportResult(
            provider,
            ModelCallResult.Succeeded(sw.ElapsedMilliseconds, inputTokens, outputTokens, cost)
        );
    }

    private ModelRoutingContext CreateRoutingContext(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions? options
    )
    {
        var inputTokens = EstimateInputTokens(messages);
        var outputTokens = options?.MaxTokens ?? 1000;

        return new ModelRoutingContext
        {
            EstimatedInputTokens = inputTokens,
            EstimatedOutputTokens = outputTokens,
            RequiresStreaming = false,
        };
    }

    private int EstimateInputTokens(IReadOnlyList<ChatMessage> messages)
    {
        if (_tokenCounter != null)
        {
            return _tokenCounter.CountTokens(messages);
        }

        // 简单估算：每 4 个字符约 1 个 token
        var totalChars = messages.Sum(m => (m.Content?.Length ?? 0) + (m.Role?.Length ?? 0));
        return totalChars / 4;
    }

    private decimal CalculateCost(string providerName, int inputTokens, int outputTokens)
    {
        var pricing = ModelPricing.KnownPricing.GetPricing(providerName);
        return pricing.CalculateCost(inputTokens, outputTokens);
    }
}

/// <summary>
/// Token 计数器接口（可选依赖）
/// </summary>
public interface ITokenCounter
{
    int CountTokens(string text);
    int CountTokens(IEnumerable<ChatMessage> messages);
}
