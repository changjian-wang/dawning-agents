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
/// 基于 Redis Pub/Sub 的分布式消息总线
/// </summary>
/// <remarks>
/// <para>点对点和广播使用 Redis Pub/Sub 实现跨进程消息传递</para>
/// <para>主题订阅使用独立的 Redis 频道前缀</para>
/// <para>请求/响应模式使用临时频道实现跨进程跨节点等待</para>
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
    /// 初始化 Redis 消息总线
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
            throw new ArgumentException("点对点消息必须指定 ReceiverId", nameof(message));
        }

        _logger.LogDebug(
            "发送消息 {MessageId}: {Sender} -> {Receiver}",
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

        _logger.LogDebug("广播消息 {MessageId} 来自 {Sender}", message.Id, message.SenderId);

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

        // 订阅点对点频道
        SubscribeToChannel(agentChannel, msg => DispatchToAgent(agentId, msg));
        // 同时订阅广播频道
        SubscribeToChannel(broadcastChannel, msg => DispatchToAgent(agentId, msg));

        _logger.LogDebug("Agent {AgentId} 订阅了消息", agentId);

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

            _logger.LogDebug("Agent {AgentId} 取消了订阅", agentId);
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

        _logger.LogDebug("Agent {AgentId} 订阅了主题 {Topic}", agentId, topic);

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

            _logger.LogDebug("Agent {AgentId} 取消订阅主题 {Topic}", agentId, topic);
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

        _logger.LogDebug("发布事件 {EventType} 到主题 {Topic}", message.EventType, topic);

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

        // 订阅响应频道
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
            // 发送请求
            await SendAsync(requestWithCorrelation, cancellationToken).ConfigureAwait(false);

            // 等待响应
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"请求 {correlationId} 等待响应超时 ({timeout})");
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
            _logger.LogWarning(ex, "反序列化 Agent 消息失败");
            return;
        }

        if (message is null)
        {
            return;
        }

        // 处理 Response 关联
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
                _logger.LogError(ex, "处理消息 {MessageId} 时出错", message.Id);
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
            _logger.LogWarning(ex, "反序列化事件消息失败");
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
                _logger.LogError(ex, "Agent {AgentId} 处理事件时出错", agentId);
            }
        }
    }

    private static string SerializeMessage(AgentMessage message) =>
        JsonSerializer.Serialize(message, message.GetType(), s_jsonOptions);

    private static AgentMessage? DeserializeMessage(string json)
    {
        // 尝试反序列化为具体类型
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 根据消息特征判断类型
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

        // 回退：尝试作为 TaskMessage（最常见）
        return JsonSerializer.Deserialize<TaskMessage>(json, s_jsonOptions);
    }
}
