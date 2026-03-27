namespace Dawning.Agents.Core.Communication;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Dawning.Agents.Abstractions.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// In-memory message bus implementation.
/// </summary>
/// <remarks>
/// Designed for intra-process agent communication. Supports:
/// <list type="bullet">
/// <item>Point-to-point messaging</item>
/// <item>Broadcast messaging</item>
/// <item>Topic-based publish/subscribe</item>
/// <item>Request/response pattern</item>
/// </list>
/// </remarks>
public sealed class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<
        string,
        ImmutableList<Action<AgentMessage>>
    > _subscribers = new();
    private readonly ConcurrentDictionary<
        string,
        ImmutableList<(string AgentId, Action<EventMessage> Handler)>
    > _topicSubscribers = new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<ResponseMessage>
    > _pendingRequests = new();
    private readonly ILogger<InMemoryMessageBus> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryMessageBus"/> class.
    /// </summary>
    public InMemoryMessageBus(ILogger<InMemoryMessageBus>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryMessageBus>.Instance;
    }

    /// <inheritdoc />
    public Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(message.ReceiverId))
        {
            throw new ArgumentException("Point-to-point messages must specify a ReceiverId.", nameof(message));
        }

        _logger.LogDebug(
            "Sending message {MessageId}: {Sender} -> {Receiver}",
            message.Id,
            message.SenderId,
            message.ReceiverId
        );

        // Deliver to the specified receiver
        if (_subscribers.TryGetValue(message.ReceiverId, out var handlers))
        {
            foreach (var handler in handlers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
                }
            }
        }

        // Correlate response messages
        if (
            message is ResponseMessage response
            && _pendingRequests.TryRemove(response.CorrelationId, out var tcs)
        )
        {
            tcs.TrySetResult(response);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting message {MessageId} from {Sender}", message.Id, message.SenderId);

        foreach (var handlers in _subscribers.Values)
        {
            foreach (var handler in handlers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing broadcast message {MessageId}", message.Id);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, Action<AgentMessage> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(handler);

        _subscribers.AddOrUpdate(
            agentId,
            _ => ImmutableList.Create(handler),
            (_, list) => list.Add(handler)
        );

        _logger.LogDebug("Agent {AgentId} subscribed to messages", agentId);

        return new Subscription(() =>
        {
            _subscribers.AddOrUpdate(
                agentId,
                _ => ImmutableList<Action<AgentMessage>>.Empty,
                (_, list) => list.Remove(handler)
            );
            _subscribers.TryRemove(
                new KeyValuePair<string, ImmutableList<Action<AgentMessage>>>(
                    agentId,
                    ImmutableList<Action<AgentMessage>>.Empty
                )
            );
            _logger.LogDebug("Agent {AgentId} unsubscribed from messages", agentId);
        });
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = (agentId, handler);

        _topicSubscribers.AddOrUpdate(
            topic,
            _ => ImmutableList.Create(subscription),
            (_, list) => list.Add(subscription)
        );

        _logger.LogDebug("Agent {AgentId} subscribed to topic {Topic}", agentId, topic);

        return new Subscription(() =>
        {
            _topicSubscribers.AddOrUpdate(
                topic,
                _ => ImmutableList<(string AgentId, Action<EventMessage> Handler)>.Empty,
                (_, list) => list.Remove(subscription)
            );
            _topicSubscribers.TryRemove(
                new KeyValuePair<
                    string,
                    ImmutableList<(string AgentId, Action<EventMessage> Handler)>
                >(topic, ImmutableList<(string AgentId, Action<EventMessage> Handler)>.Empty)
            );
            _logger.LogDebug("Agent {AgentId} unsubscribed from topic {Topic}", agentId, topic);
        });
    }

    /// <inheritdoc />
    public Task PublishAsync(
        string topic,
        EventMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(message);
        _logger.LogDebug("Publishing event {EventType} to topic {Topic}", message.EventType, topic);

        if (_topicSubscribers.TryGetValue(topic, out var subscribers))
        {
            foreach (var (agentId, handler) in subscribers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event for agent {AgentId}", agentId);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var correlationId = request.CorrelationId ?? request.Id;
        var tcs = new TaskCompletionSource<ResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        if (!_pendingRequests.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"Request {correlationId} already exists.");
        }

        try
        {
            // Send the request
            await SendAsync(request with { CorrelationId = correlationId }, cancellationToken)
                .ConfigureAwait(false);

            // Wait for the response or timeout
            try
            {
                return await tcs.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException(
                    $"Request {correlationId} timed out after {timeout.TotalSeconds} seconds.",
                    ex
                );
            }
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    /// <summary>
    /// Gets the number of subscribers.
    /// </summary>
    public int SubscriberCount => _subscribers.Count;

    /// <summary>
    /// Gets the number of topics.
    /// </summary>
    public int TopicCount => _topicSubscribers.Count;

    /// <summary>
    /// Subscription disposer.
    /// </summary>
    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private volatile bool _disposed;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
