using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// Adaptive memory that automatically downgrades from <see cref="BufferMemory"/> to <see cref="SummaryMemory"/> based on token usage.
/// </summary>
/// <remarks>
/// <para>Initially uses <see cref="BufferMemory"/> to store complete messages.</para>
/// <para>Automatically switches to <see cref="SummaryMemory"/> when the token count exceeds the threshold.</para>
/// <para>Migrates existing messages to <see cref="SummaryMemory"/> and triggers summarization on switch.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public sealed class AdaptiveMemory : IConversationMemory, IDisposable
{
    private IConversationMemory _currentMemory;
    private readonly ILLMProvider _llm;
    private readonly ITokenCounter _tokenCounter;
    private readonly int _downgradeThreshold;
    private readonly int _maxRecentMessages;
    private readonly int _summaryThreshold;
    private readonly ILogger<AdaptiveMemory> _logger;
    private readonly SemaphoreSlim _downgradeLock = new(1, 1);
    private bool _hasDowngraded;
    private volatile bool _disposed;

    /// <summary>
    /// Gets the current stored message count.
    /// </summary>
    public int MessageCount => Volatile.Read(ref _currentMemory).MessageCount;

    /// <summary>
    /// Gets a value indicating whether the memory has downgraded to <see cref="SummaryMemory"/>.
    /// </summary>
    public bool HasDowngraded => Volatile.Read(ref _hasDowngraded);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveMemory"/> class.
    /// </summary>
    /// <param name="llm">The LLM provider used to generate summaries.</param>
    /// <param name="tokenCounter">The token counter.</param>
    /// <param name="downgradeThreshold">The token threshold that triggers downgrade. Defaults to 4000.</param>
    /// <param name="maxRecentMessages">The number of recent messages to retain after downgrade. Defaults to 6.</param>
    /// <param name="summaryThreshold">The message count threshold that triggers summarization after downgrade. Defaults to 10.</param>
    /// <param name="logger">An optional logger instance.</param>
    public AdaptiveMemory(
        ILLMProvider llm,
        ITokenCounter tokenCounter,
        int downgradeThreshold = 4000,
        int maxRecentMessages = 6,
        int summaryThreshold = 10,
        ILogger<AdaptiveMemory>? logger = null
    )
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _downgradeThreshold =
            downgradeThreshold > 0
                ? downgradeThreshold
                : throw new ArgumentException(
                    "Downgrade threshold must be a positive number.",
                    nameof(downgradeThreshold)
                );
        _maxRecentMessages =
            maxRecentMessages > 0
                ? maxRecentMessages
                : throw new ArgumentException(
                    "Max recent messages must be a positive number.",
                    nameof(maxRecentMessages)
                );
        _summaryThreshold =
            summaryThreshold > maxRecentMessages
                ? summaryThreshold
                : throw new ArgumentException(
                    "Summary threshold must be greater than max recent messages.",
                    nameof(summaryThreshold)
                );
        _logger = logger ?? NullLogger<AdaptiveMemory>.Instance;

        // Initially use BufferMemory
        _currentMemory = new BufferMemory(tokenCounter);
        _hasDowngraded = false;

        _logger.LogDebug(
            "AdaptiveMemory initialized, Downgrade threshold: {Threshold} tokens",
            _downgradeThreshold
        );
    }

    /// <summary>
    /// Adds a message and automatically downgrades to <see cref="SummaryMemory"/> when the threshold is exceeded.
    /// </summary>
    public async Task AddMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _downgradeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var currentMemory = _currentMemory;
            await currentMemory.AddMessageAsync(message, cancellationToken).ConfigureAwait(false);

            // Check if downgrade is needed
            if (!Volatile.Read(ref _hasDowngraded))
            {
                var totalTokens = await currentMemory
                    .GetTokenCountAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (totalTokens >= _downgradeThreshold)
                {
                    await DowngradeToSummaryMemoryCoreAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _downgradeLock.Release();
        }
    }

    /// <summary>
    /// Downgrades to <see cref="SummaryMemory"/> (caller must hold _downgradeLock).
    /// </summary>
    private async Task DowngradeToSummaryMemoryCoreAsync(CancellationToken cancellationToken)
    {
        if (_hasDowngraded)
        {
            return;
        }

        var currentTokens = await _currentMemory
            .GetTokenCountAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Automatic downgrade triggered: switching from BufferMemory to SummaryMemory (current tokens: {Tokens})",
            currentTokens
        );

        // Get existing messages
        var existingMessages = await _currentMemory
            .GetMessagesAsync(cancellationToken)
            .ConfigureAwait(false);

        // Create SummaryMemory (ILogger<AdaptiveMemory> cannot be converted to ILogger<SummaryMemory>, so pass null explicitly and let SummaryMemory fall back to NullLogger)
        var summaryMemory = new SummaryMemory(
            _llm,
            _tokenCounter,
            _maxRecentMessages,
            _summaryThreshold
        );

        // Migrate messages to SummaryMemory (this automatically triggers summarization)
        foreach (var msg in existingMessages)
        {
            await summaryMemory.AddMessageAsync(msg, cancellationToken).ConfigureAwait(false);
        }

        // Switch to SummaryMemory
        Volatile.Write(ref _currentMemory, summaryMemory);
        Volatile.Write(ref _hasDowngraded, true);

        _logger.LogInformation(
            "Downgrade complete, new token count: {NewTokens}",
            await summaryMemory.GetTokenCountAsync(cancellationToken).ConfigureAwait(false)
        );
    }

    /// <summary>
    /// Gets all messages.
    /// </summary>
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetMessagesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the message context for LLM calls.
    /// </summary>
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(
        int? maxTokens = null,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetContextAsync(maxTokens, cancellationToken);
    }

    /// <summary>
    /// Clears all messages and resets to <see cref="BufferMemory"/>.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _downgradeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var oldMemory = _currentMemory;
            await oldMemory.ClearAsync(cancellationToken).ConfigureAwait(false);

            // Reset to BufferMemory
            Volatile.Write(ref _currentMemory, new BufferMemory(_tokenCounter));
            Volatile.Write(ref _hasDowngraded, false);

            // Do not immediately Dispose old memory to avoid races with concurrent read paths (let GC collect it)
        }
        finally
        {
            _downgradeLock.Release();
        }

        _logger.LogDebug("AdaptiveMemory cleared and reset to BufferMemory");
    }

    /// <summary>
    /// Gets the current total token count.
    /// </summary>
    public Task<int> GetTokenCountAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Volatile.Read(ref _currentMemory).GetTokenCountAsync(cancellationToken);
    }

    /// <summary>
    /// Releases the semaphore and internal memory resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downgradeLock.Dispose();
        (_currentMemory as IDisposable)?.Dispose();
    }
}
