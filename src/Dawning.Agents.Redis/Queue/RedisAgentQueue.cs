using System.Collections.Concurrent;
using System.Text.Json;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Scaling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dawning.Agents.Redis.Queue;

/// <summary>
/// 基于 Redis Streams 的分布式 Agent 队列
/// </summary>
/// <remarks>
/// <para>使用 Redis Streams 实现高性能分布式队列</para>
/// <para>支持消费者组、消息确认、死信队列等特性</para>
/// </remarks>
public sealed class RedisAgentQueue : IDistributedAgentQueue, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly DistributedQueueOptions _options;
    private readonly RedisOptions _redisOptions;
    private readonly ILogger<RedisAgentQueue> _logger;
    private readonly string _queueKey;
    private readonly string _deadLetterKey;
    private readonly string _consumerName;
    private int _count;
    private volatile bool _initialized;
    private volatile bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentDictionary<
        string,
        (RedisValue StreamId, string Data)
    > _messageIdToStreamEntry = new();

    /// <inheritdoc />
    public string ConsumerGroup => _options.ConsumerGroup;

    /// <inheritdoc />
    public string ConsumerName => _consumerName;

    /// <inheritdoc />
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc />
    public bool CanWrite => !_disposed;

    /// <summary>
    /// 初始化 Redis Agent 队列
    /// </summary>
    public RedisAgentQueue(
        IConnectionMultiplexer connection,
        IOptions<DistributedQueueOptions> queueOptions,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisAgentQueue>? logger = null
    )
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = queueOptions?.Value ?? throw new ArgumentNullException(nameof(queueOptions));
        _redisOptions =
            redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _logger = logger ?? NullLogger<RedisAgentQueue>.Instance;

        _database = _connection.GetDatabase(_redisOptions.DefaultDatabase);
        _queueKey = $"{_redisOptions.InstanceName}{_options.QueueName}";
        _deadLetterKey = $"{_redisOptions.InstanceName}{_options.DeadLetterQueue}";
        _consumerName =
            $"{_options.ConsumerNamePrefix}-{Environment.MachineName}-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 确保消费者组已创建
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                // 尝试创建消费者组，如果已存在则忽略错误
                await _database
                    .StreamCreateConsumerGroupAsync(_queueKey, _options.ConsumerGroup, "0-0", true)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Created consumer group {Group} for queue {Queue}",
                    _options.ConsumerGroup,
                    _queueKey
                );
            }
            catch (RedisServerException ex)
                when (ex.Message.Contains("BUSYGROUP", StringComparison.Ordinal))
            {
                // 消费者组已存在，忽略
                _logger.LogDebug("Consumer group {Group} already exists", _options.ConsumerGroup);
            }

            _initialized = true;

            // Reconcile local count from Redis XLEN
            var length = await _database.StreamLengthAsync(_queueKey).ConfigureAwait(false);
            Interlocked.Exchange(ref _count, (int)Math.Min(length, int.MaxValue));
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(
        AgentWorkItem item,
        CancellationToken cancellationToken = default
    )
    {
        await EnqueueWithIdAsync(item, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> EnqueueWithIdAsync(
        AgentWorkItem item,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var message = new DistributedQueueMessage
        {
            MessageId = item.Id,
            WorkItem = item,
            EnqueuedAt = DateTimeOffset.UtcNow,
            RetryCount = 0,
            MaxRetries = _options.MaxRetries,
        };

        var serialized = JsonSerializer.Serialize(message);

        try
        {
            if (delay.HasValue)
            {
                // 使用 Sorted Set 实现延迟队列
                var delayKey = $"{_queueKey}:delayed";
                var executeAt = DateTimeOffset.UtcNow.Add(delay.Value).ToUnixTimeMilliseconds();
                await _database
                    .SortedSetAddAsync(delayKey, serialized, executeAt)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Enqueued delayed message {MessageId}, delay: {Delay}",
                    item.Id,
                    delay.Value
                );
            }
            else
            {
                var streamId = await _database
                    .StreamAddAsync(
                        _queueKey,
                        new NameValueEntry[]
                        {
                            new("data", serialized),
                            new("priority", item.Priority),
                        }
                    )
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Enqueued message {MessageId} with stream ID {StreamId}",
                    item.Id,
                    streamId
                );
            }

            Interlocked.Increment(ref _count);
            return item.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue message {MessageId}", item.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<AgentWorkItem?> DequeueAsync(
        CancellationToken cancellationToken = default
    )
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 先处理延迟队列中到期的消息
            await ProcessDelayedMessagesAsync(cancellationToken).ConfigureAwait(false);

            // 从 Stream 读取消息
            var entries = await _database
                .StreamReadGroupAsync(_queueKey, _options.ConsumerGroup, _consumerName, ">", 1)
                .ConfigureAwait(false);

            if (entries.Length == 0)
            {
                return null;
            }

            var entry = entries[0];
            var data = entry.Values.FirstOrDefault(v => v.Name == "data").Value;

            if (data.IsNullOrEmpty)
            {
                // ACK 损坏的消息并移入死信队列
                await _database
                    .StreamAcknowledgeAsync(_queueKey, _options.ConsumerGroup, entry.Id)
                    .ConfigureAwait(false);
                await MoveToDeadLetterAsync(
                        entry.Id.ToString(),
                        "Empty data field",
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                Interlocked.Decrement(ref _count);
                return null;
            }

            var message = JsonSerializer.Deserialize<DistributedQueueMessage>(data.ToString());
            if (message == null)
            {
                // ACK 损坏的消息并移入死信队列
                await _database
                    .StreamAcknowledgeAsync(_queueKey, _options.ConsumerGroup, entry.Id)
                    .ConfigureAwait(false);
                await MoveToDeadLetterAsync(
                        entry.Id.ToString(),
                        "Deserialization returned null",
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                Interlocked.Decrement(ref _count);
                return null;
            }

            // 记录 messageId → (streamId, data) 映射，供 AcknowledgeAsync/RequeueAsync 使用
            _messageIdToStreamEntry[message.MessageId] = (entry.Id, data.ToString());

            _logger.LogDebug(
                "Dequeued message {MessageId} with stream ID {StreamId}",
                message.MessageId,
                entry.Id
            );

            Interlocked.Decrement(ref _count);
            return message.WorkItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue message");
            throw;
        }
    }

    /// <summary>
    /// 处理延迟队列中到期的消息
    /// </summary>
    private async Task ProcessDelayedMessagesAsync(CancellationToken cancellationToken = default)
    {
        var delayKey = $"{_queueKey}:delayed";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 获取所有到期的消息
        var entries = await _database
            .SortedSetRangeByScoreAsync(delayKey, 0, now, take: 10)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 移动到主队列
            var removed = await _database
                .SortedSetRemoveAsync(delayKey, entry)
                .ConfigureAwait(false);
            if (removed)
            {
                await _database
                    .StreamAddAsync(
                        _queueKey,
                        new NameValueEntry[] { new("data", entry), new("priority", 0) }
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(
        string messageId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        try
        {
            if (_messageIdToStreamEntry.TryRemove(messageId, out var entry))
            {
                await _database
                    .StreamAcknowledgeAsync(_queueKey, _options.ConsumerGroup, entry.StreamId)
                    .ConfigureAwait(false);

                _logger.LogDebug(
                    "Acknowledged message {MessageId} with stream ID {StreamId}",
                    messageId,
                    entry.StreamId
                );
            }
            else
            {
                _logger.LogWarning(
                    "Cannot acknowledge message {MessageId}: stream ID mapping not found",
                    messageId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge message {MessageId}", messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RequeueAsync(
        string messageId,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        try
        {
            // 先确认原消息，防止 PEL 积压
            if (!_messageIdToStreamEntry.TryRemove(messageId, out var entry))
            {
                _logger.LogWarning("Cannot requeue unknown message {MessageId}", messageId);
                return;
            }

            await _database
                .StreamAcknowledgeAsync(_queueKey, _options.ConsumerGroup, entry.StreamId)
                .ConfigureAwait(false);

            // 反序列化以递增 RetryCount，检查是否超过上限
            var message = JsonSerializer.Deserialize<DistributedQueueMessage>(entry.Data);
            if (message != null)
            {
                message = message with { RetryCount = message.RetryCount + 1 };
                if (message.RetryCount >= message.MaxRetries)
                {
                    await MoveToDeadLetterAsync(
                            messageId,
                            $"Max retries exceeded ({message.MaxRetries})",
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    return;
                }
            }

            var requeueData = message != null ? JsonSerializer.Serialize(message) : entry.Data;

            // 重新入队
            if (delay.HasValue)
            {
                var delayKey = $"{_queueKey}:delayed";
                var executeAt = DateTimeOffset.UtcNow.Add(delay.Value).ToUnixTimeMilliseconds();
                await _database
                    .SortedSetAddAsync(delayKey, requeueData, executeAt)
                    .ConfigureAwait(false);
            }
            else
            {
                await _database
                    .StreamAddAsync(
                        _queueKey,
                        new NameValueEntry[] { new("data", requeueData), new("priority", 0) }
                    )
                    .ConfigureAwait(false);
            }

            Interlocked.Increment(ref _count);

            _logger.LogDebug("Requeued message {MessageId} with delay {Delay}", messageId, delay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to requeue message {MessageId}", messageId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task MoveToDeadLetterAsync(
        string messageId,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var deadLetterEntry = new
            {
                MessageId = messageId,
                Reason = reason,
                MovedAt = DateTimeOffset.UtcNow,
            };

            await _database
                .ListRightPushAsync(_deadLetterKey, JsonSerializer.Serialize(deadLetterEntry))
                .ConfigureAwait(false);

            // Cap dead letter queue size and set TTL to prevent unbounded growth
            await _database.ListTrimAsync(_deadLetterKey, -10_000, -1).ConfigureAwait(false);
            await _database
                .KeyExpireAsync(_deadLetterKey, TimeSpan.FromDays(7), ExpireWhen.HasNoExpiry)
                .ConfigureAwait(false);

            _logger.LogWarning(
                "Moved message {MessageId} to dead letter queue. Reason: {Reason}",
                messageId,
                reason
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move message {MessageId} to dead letter queue",
                messageId
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _database.StreamInfoAsync(_queueKey).ConfigureAwait(false);
            return info.Length;
        }
        catch (RedisServerException)
        {
            // Stream 不存在
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetDeadLetterCountAsync(CancellationToken cancellationToken = default)
    {
        return await _database.ListLengthAsync(_deadLetterKey).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _initLock.Dispose();
        _logger.LogDebug("Disposed Redis agent queue");
        return ValueTask.CompletedTask;
    }
}
