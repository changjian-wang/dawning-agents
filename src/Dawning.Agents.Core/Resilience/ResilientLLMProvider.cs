using System.Runtime.CompilerServices;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Resilience;

/// <summary>
/// Resilient LLM provider decorator.
/// </summary>
/// <remarks>
/// Wraps <see cref="ILLMProvider"/> to apply retry, circuit breaker, timeout, and other resilience strategies to all calls.
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

        _logger.LogDebug("ResilientLLMProvider wrapping {InnerProvider}", innerProvider.Name);
    }

    public async Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Executing ChatAsync with resilience strategy");

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
        _logger.LogDebug("Executing ChatStreamAsync with resilience strategy");

        // Streaming responses require special handling: probe the first element inside the
        // resilience strategy to detect connection/authentication errors. Once the stream starts,
        // subsequent elements are not protected by the resilience strategy (partial streams cannot be retried).
        IAsyncEnumerator<string>? enumerator = null;
        string? firstElement = null;
        var hasFirst = false;

        try
        {
            await _resilienceProvider
                .ExecuteAsync(
                    async ct =>
                    {
                        // Dispose the previous enumerator on retry
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

                        // Probe the first element: MoveNextAsync triggers actual I/O (HTTP connection, authentication, etc.);
                        // WaitAsync(ct) allows the Polly timeout strategy to interrupt the probe
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

            // Yield the probed first element
            if (hasFirst)
            {
                yield return firstElement!;
            }

            // Continue iterating remaining elements
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
        _logger.LogDebug("Executing ChatStreamEventsAsync with resilience strategy");

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
