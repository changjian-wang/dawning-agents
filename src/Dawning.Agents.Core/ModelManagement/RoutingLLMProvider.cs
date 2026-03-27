using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.ModelManagement;

/// <summary>
/// Routing-enabled LLM provider.
/// </summary>
/// <remarks>
/// Wraps <see cref="IModelRouter"/> as an <see cref="ILLMProvider"/>, supporting:
/// <list type="bullet">
///   <item>Automatic route selection</item>
///   <item>Failover</item>
///   <item>Call statistics</item>
/// </list>
/// </remarks>
public sealed class RoutingLLMProvider : ILLMProvider
{
    private readonly Lazy<IModelRouter> _lazyRouter;
    private readonly ModelRouterOptions _options;
    private readonly ILogger<RoutingLLMProvider> _logger;
    private readonly ITokenCounter? _tokenCounter;

    private IModelRouter Router => _lazyRouter.Value;

    public string Name => $"Routing({Router.Name})";

    public RoutingLLMProvider(
        IModelRouter router,
        IOptions<ModelRouterOptions> options,
        ILogger<RoutingLLMProvider>? logger = null,
        ITokenCounter? tokenCounter = null
    )
        : this(
            new Lazy<IModelRouter>(router ?? throw new ArgumentNullException(nameof(router))),
            options,
            logger,
            tokenCounter
        ) { }

    /// <summary>
    /// Lazy-resolution constructor for DI registration, breaking the
    /// IModelRouter → IEnumerable&lt;ILLMProvider&gt; → RoutingLLMProvider → IModelRouter circular dependency.
    /// </summary>
    internal RoutingLLMProvider(
        Lazy<IModelRouter> lazyRouter,
        IOptions<ModelRouterOptions> options,
        ILogger<RoutingLLMProvider>? logger = null,
        ITokenCounter? tokenCounter = null
    )
    {
        _lazyRouter = lazyRouter ?? throw new ArgumentNullException(nameof(lazyRouter));
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
                provider = await Router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("All providers excluded; resetting exclusion list and retrying");
                excludedProviders.Clear();
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogDebug(
                    "Attempt {Attempt}/{MaxRetries} using provider: {Provider}",
                    attempt + 1,
                    maxRetries + 1,
                    provider.Name
                );

                var response = await provider
                    .ChatAsync(messageList, options, cancellationToken)
                    .ConfigureAwait(false);
                sw.Stop();

                // Report success
                var cost = CalculateCost(
                    provider.Name,
                    response.PromptTokens,
                    response.CompletionTokens
                );
                Router.ReportResult(
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
                _logger.LogWarning(ex, "Provider {Provider} call failed; attempting failover", provider.Name);

                // Report failure
                Router.ReportResult(
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
                provider = await Router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("All providers excluded; resetting exclusion list and retrying");
                excludedProviders.Clear();
                continue;
            }

            _logger.LogDebug(
                "Streaming attempt {Attempt}/{MaxRetries} using provider: {Provider}",
                attempt + 1,
                maxRetries + 1,
                provider.Name
            );

            // Try to get the stream
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
                    "Streaming provider {Provider} initialization failed; attempting failover",
                    provider.Name
                );
                Router.ReportResult(provider, ModelCallResult.Failed(error.Message, 0));
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
                provider = await Router
                    .SelectProviderAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxRetries)
            {
                _logger.LogWarning("All providers excluded; resetting exclusion list and retrying");
                excludedProviders.Clear();
                continue;
            }

            _logger.LogDebug(
                "Event stream attempt {Attempt}/{MaxRetries} using provider: {Provider}",
                attempt + 1,
                maxRetries + 1,
                provider.Name
            );

            // Async iterator try/catch cannot catch deferred execution errors;
            // must probe the first element via TryGetEventStreamAsync to detect actual errors
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
                    "Event stream provider {Provider} initialization failed; attempting failover",
                    provider.Name
                );
                Router.ReportResult(provider, ModelCallResult.Failed(error.Message, 0));
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
            // Async iterator methods execute no code when called; errors are deferred to the first MoveNextAsync,
    // so ProbeStreamAsync must probe the first element to detect connection/authentication errors
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
    /// Probes the first element of an async stream to detect provider connection/authentication errors
    /// before iteration. Returns a combined stream on success; returns the exception on failure.
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
    /// Merges an already-fetched first element with the remaining enumerator into a single async stream.
    /// After the caller obtains first via ProbeStreamAsync, ownership is transferred to this method for disposal.
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

        // Estimate output tokens (stream responses don't provide exact token counts)
        var outputTokens = totalChars / 4;
        var inputTokens = EstimateInputTokens(messages);
        var cost = CalculateCost(provider.Name, inputTokens, outputTokens);
        Router.ReportResult(
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
        Router.ReportResult(
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

        // Simple estimate: ~1 token per 4 characters
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
/// Token counter interface (optional dependency).
/// </summary>
public interface ITokenCounter
{
    int CountTokens(string text);
    int CountTokens(IEnumerable<ChatMessage> messages);
}
