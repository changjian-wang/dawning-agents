namespace Dawning.Agents.Core.Communication;

using System.Collections.Concurrent;
using Dawning.Agents.Abstractions.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// 内存消息总线实现
/// </summary>
/// <remarks>
/// 适用于单进程内的 Agent 通信，支持：
/// <list type="bullet">
/// <item>点对点消息</item>
/// <item>广播消息</item>
/// <item>主题订阅/发布</item>
/// <item>请求/响应模式</item>
/// </list>
/// </remarks>
public class InMemoryMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, List<Action<AgentMessage>>> _subscribers = new();
    private readonly ConcurrentDictionary<
        string,
        List<(string AgentId, Action<EventMessage> Handler)>
    > _topicSubscribers = new();
    private readonly ConcurrentDictionary<
        string,
        TaskCompletionSource<ResponseMessage>
    > _pendingRequests = new();
    private readonly ILogger<InMemoryMessageBus> _logger;
    private readonly object _subscriberLock = new();
    private readonly object _topicLock = new();

    /// <summary>
    /// 创建内存消息总线
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
            throw new ArgumentException("点对点消息必须指定 ReceiverId", nameof(message));
        }

        _logger.LogDebug(
            "发送消息 {MessageId}: {Sender} -> {Receiver}",
            message.Id,
            message.SenderId,
            message.ReceiverId
        );

        // 发送给指定接收者
        if (_subscribers.TryGetValue(message.ReceiverId, out var handlers))
        {
            List<Action<AgentMessage>> handlersCopy;
            lock (_subscriberLock)
            {
                handlersCopy = handlers.ToList();
            }

            foreach (var handler in handlersCopy)
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

        // 处理响应消息的关联
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
        _logger.LogDebug("广播消息 {MessageId} 来自 {Sender}", message.Id, message.SenderId);

        List<List<Action<AgentMessage>>> allHandlers;
        lock (_subscriberLock)
        {
            allHandlers = _subscribers.Values.Select(h => h.ToList()).ToList();
        }

        foreach (var handlers in allHandlers)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理广播消息 {MessageId} 时出错", message.Id);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, Action<AgentMessage> handler)
    {
        var handlers = _subscribers.GetOrAdd(agentId, _ => new List<Action<AgentMessage>>());

        lock (_subscriberLock)
        {
            handlers.Add(handler);
        }

        _logger.LogDebug("Agent {AgentId} 订阅了消息", agentId);

        return new Subscription(() =>
        {
            lock (_subscriberLock)
            {
                handlers.Remove(handler);
            }
            _logger.LogDebug("Agent {AgentId} 取消了订阅", agentId);
        });
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler)
    {
        var subscribers = _topicSubscribers.GetOrAdd(
            topic,
            _ => new List<(string, Action<EventMessage>)>()
        );

        var subscription = (agentId, handler);

        lock (_topicLock)
        {
            subscribers.Add(subscription);
        }

        _logger.LogDebug("Agent {AgentId} 订阅了主题 {Topic}", agentId, topic);

        return new Subscription(() =>
        {
            lock (_topicLock)
            {
                subscribers.Remove(subscription);
            }
            _logger.LogDebug("Agent {AgentId} 取消订阅主题 {Topic}", agentId, topic);
        });
    }

    /// <inheritdoc />
    public Task PublishAsync(
        string topic,
        EventMessage message,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "发布事件 {EventType} 到主题 {Topic}",
            message.EventType,
            topic
        );

        if (_topicSubscribers.TryGetValue(topic, out var subscribers))
        {
            List<(string AgentId, Action<EventMessage> Handler)> subscribersCopy;
            lock (_topicLock)
            {
                subscribersCopy = subscribers.ToList();
            }

            foreach (var (agentId, handler) in subscribersCopy)
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
            throw new InvalidOperationException($"请求 {correlationId} 已存在");
        }

        try
        {
            // 发送请求
            await SendAsync(request with { CorrelationId = correlationId }, cancellationToken);

            // 等待响应或超时
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var completedTask = await Task.WhenAny(
                tcs.Task,
                Task.Delay(Timeout.Infinite, cts.Token)
            );

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            throw new TimeoutException($"请求 {correlationId} 在 {timeout.TotalSeconds} 秒后超时");
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    /// <summary>
    /// 获取订阅者数量
    /// </summary>
    public int SubscriberCount => _subscribers.Count;

    /// <summary>
    /// 获取主题数量
    /// </summary>
    public int TopicCount => _topicSubscribers.Count;

    /// <summary>
    /// 订阅取消器
    /// </summary>
    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

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
