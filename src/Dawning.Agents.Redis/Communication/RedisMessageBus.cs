using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Dawning.Agents.Abstractions.Communication;
using Dawning.Agents.Abstractions.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Communication;

/// <summary>
/// A distributed message bus backed by Redis Pub/Sub.
/// </summary>
/// <remarks>
/// <para>Point-to-point and broadcast messaging uses Redis Pub/Sub for cross-process message delivery.</para>
/// <para>Topic subscriptions use a dedicated Redis channel prefix.</para>
/// <para>Request/response pattern uses temporary channels for cross-process, cross-node awaiting.</para>
/// </remarks>
public sealed class RedisMessageBus : IMessageBus, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ISubscriber _subscriber;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisMessageBus> _logger;
    private readonly string _prefix;

    private readonly ConcurrentDictionary<
        string,
        ImmutableList<Action<AgentMessage>>
    > _agentHandlers = new();
    private readonly ConcurrentDictionary<
        string,
        ImmutableList<(string AgentId, Action<EventMessage> Handler)>
    > _topicHandlers = new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<ResponseMessage>
    > _pendingRequests = new();

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisMessageBus"/> class.
    /// </summary>
    public RedisMessageBus(
        IConnectionMultiplexer connection,
        IOptions<RedisOptions> options,
        ILogger<RedisMessageBus>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger ?? NullLogger<RedisMessageBus>.Instance;
        _subscriber = _connection.GetSubscriber();
        _prefix = $"{_options.InstanceName}msgbus:";
    }

    /// <inheritdoc />
    public async Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.ReceiverId))
        {
            throw new ArgumentException("ReceiverId is required for point-to-point messages.", nameof(message));
        }

        _logger.LogDebug(
            "Sending message {MessageId}: {Sender} -> {Receiver}",
            message.Id,
            message.SenderId,
            message.ReceiverId
        );

        var channel = RedisChannel.Literal($"{_prefix}agent:{message.ReceiverId}");
        var json = SerializeMessage(message);

        await _subscriber.PublishAsync(channel, json).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(
        AgentMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

                _logger.LogDebug("Broadcasting message {MessageId} from {Sender}", message.Id, message.SenderId);

        var channel = RedisChannel.Literal($"{_prefix}broadcast");
        var json = SerializeMessage(message);

        await _subscriber.PublishAsync(channel, json).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, Action<AgentMessage> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(handler);

        _agentHandlers.AddOrUpdate(
            agentId,
            _ => ImmutableList.Create(handler),
            (_, list) => list.Add(handler)
        );

        var agentChannel = RedisChannel.Literal($"{_prefix}agent:{agentId}");
        var broadcastChannel = RedisChannel.Literal($"{_prefix}broadcast");

        // Subscribe to the point-to-point channel
        SubscribeToChannel(agentChannel, msg => DispatchToAgent(agentId, msg));
        // Also subscribe to the broadcast channel
        SubscribeToChannel(broadcastChannel, msg => DispatchToAgent(agentId, msg));

        _logger.LogDebug("Agent {AgentId} subscribed to messages", agentId);

        return new Subscription(() =>
        {
            _agentHandlers.AddOrUpdate(
                agentId,
                _ => ImmutableList<Action<AgentMessage>>.Empty,
                (_, list) => list.Remove(handler)
            );

            if (_agentHandlers.TryGetValue(agentId, out var remaining) && remaining.Count == 0)
            {
                _agentHandlers.TryRemove(agentId, out _);
                _subscriber.Unsubscribe(agentChannel);
            }

            _logger.LogDebug("Agent {AgentId} unsubscribed from messages", agentId);
        });
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = (agentId, handler);

        _topicHandlers.AddOrUpdate(
            topic,
            _ => ImmutableList.Create(subscription),
            (_, list) => list.Add(subscription)
        );

        var channel = RedisChannel.Literal($"{_prefix}topic:{topic}");
        SubscribeToChannel(channel, msg => DispatchToTopic(topic, msg));

        _logger.LogDebug("Agent {AgentId} subscribed to topic {Topic}", agentId, topic);

        return new Subscription(() =>
        {
            _topicHandlers.AddOrUpdate(
                topic,
                _ => ImmutableList<(string, Action<EventMessage>)>.Empty,
                (_, list) => list.Remove(subscription)
            );

            if (_topicHandlers.TryGetValue(topic, out var remaining) && remaining.Count == 0)
            {
                _topicHandlers.TryRemove(topic, out _);
                _subscriber.Unsubscribe(channel);
            }

            _logger.LogDebug("Agent {AgentId} unsubscribed from topic {Topic}", agentId, topic);
        });
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        EventMessage message,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug("Publishing event {EventType} to topic {Topic}", message.EventType, topic);

        var channel = RedisChannel.Literal($"{_prefix}topic:{topic}");
        var json = SerializeMessage(message);

        await _subscriber.PublishAsync(channel, json).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = request.CorrelationId ?? Guid.NewGuid().ToString();
        var requestWithCorrelation = request with { CorrelationId = correlationId };

        var tcs = new TaskCompletionSource<ResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingRequests[correlationId] = tcs;

        // Subscribe to the response channel
        var responseChannel = RedisChannel.Literal($"{_prefix}response:{correlationId}");
        _subscriber.Subscribe(
            responseChannel,
            (_, value) =>
            {
                if (_pendingRequests.TryRemove(correlationId, out var pendingTcs) && value.HasValue)
                {
                    try
                    {
                        var response = JsonSerializer.Deserialize<ResponseMessage>(
                            value.ToString(),
                            s_jsonOptions
                        );
                        if (response is not null)
                        {
                            pendingTcs.TrySetResult(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        pendingTcs.TrySetException(ex);
                    }
                }
            }
        );

        try
        {
            // Send the request
            await SendAsync(requestWithCorrelation, cancellationToken).ConfigureAwait(false);

            // Wait for the response
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Request {correlationId} timed out waiting for response ({timeout})");
            }
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
            _subscriber.Unsubscribe(responseChannel);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }

        _pendingRequests.Clear();
        _subscriber.UnsubscribeAll();
    }

    private void SubscribeToChannel(RedisChannel channel, Action<string> onMessage)
    {
        _subscriber.Subscribe(
            channel,
            (_, value) =>
            {
                if (value.HasValue)
                {
                    onMessage(value.ToString());
                }
            }
        );
    }

    private void DispatchToAgent(string agentId, string json)
    {
        if (!_agentHandlers.TryGetValue(agentId, out var handlers))
        {
            return;
        }

        AgentMessage? message;
        try
        {
            message = DeserializeMessage(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize agent message");
            return;
        }

        if (message is null)
        {
            return;
        }

        // Handle Response correlation
        if (
            message is ResponseMessage response
            && _pendingRequests.TryRemove(response.CorrelationId, out var tcs)
        )
        {
            tcs.TrySetResult(response);
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message {MessageId}", message.Id);
            }
        }
    }

    private void DispatchToTopic(string topic, string json)
    {
        if (!_topicHandlers.TryGetValue(topic, out var subscribers))
        {
            return;
        }

        EventMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<EventMessage>(json, s_jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize event message");
            return;
        }

        if (message is null)
        {
            return;
        }

        foreach (var (agentId, handler) in subscribers)
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent {AgentId} error handling event", agentId);
            }
        }
    }

    private static string SerializeMessage(AgentMessage message) =>
        JsonSerializer.Serialize(message, message.GetType(), s_jsonOptions);

    private static AgentMessage? DeserializeMessage(string json)
    {
        // Try to deserialize to a concrete type
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Determine message type based on message characteristics
        if (root.TryGetProperty("correlationId", out _) && root.TryGetProperty("result", out _))
        {
            return JsonSerializer.Deserialize<ResponseMessage>(json, s_jsonOptions);
        }

        if (root.TryGetProperty("eventType", out _) && root.TryGetProperty("payload", out _))
        {
            return JsonSerializer.Deserialize<EventMessage>(json, s_jsonOptions);
        }

        if (root.TryGetProperty("task", out _))
        {
            return JsonSerializer.Deserialize<TaskMessage>(json, s_jsonOptions);
        }

        // Fallback: try as TaskMessage (most common)
        return JsonSerializer.Deserialize<TaskMessage>(json, s_jsonOptions);
    }
}
