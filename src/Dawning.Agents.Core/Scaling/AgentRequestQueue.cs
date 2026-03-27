namespace Dawning.Agents.Core.Scaling;

using System.Threading.Channels;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Agent request queue implementation.
/// </summary>
public sealed class AgentRequestQueue : IAgentRequestQueue
{
    private readonly Channel<AgentWorkItem> _channel;
    private readonly ILogger<AgentRequestQueue> _logger;

    public AgentRequestQueue(int capacity, ILogger<AgentRequestQueue>? logger = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _channel = Channel.CreateBounded<AgentWorkItem>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            }
        );
        _logger = logger ?? NullLogger<AgentRequestQueue>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(
        AgentWorkItem item,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Work item {WorkItemId} enqueued", item.Id);
        }
        catch (ChannelClosedException)
        {
            throw new InvalidOperationException("Request queue is closed; cannot enqueue");
        }
    }

    /// <inheritdoc />
    public async ValueTask<AgentWorkItem?> DequeueAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var item = await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Work item {WorkItemId} dequeued", item.Id);
            return item;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            // Queue is closed; throw cancellation to let the caller (worker loop) exit
            // instead of returning null which would cause a busy-loop
            throw new OperationCanceledException("Request queue is closed");
        }
    }

    /// <inheritdoc />
    public int Count => _channel.Reader.Count;

    /// <inheritdoc />
    public bool CanWrite => !_channel.Reader.Completion.IsCompleted;

    /// <summary>
    /// Closes the queue.
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
        _logger.LogInformation("Request queue closed");
    }
}
