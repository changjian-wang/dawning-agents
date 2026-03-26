using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;

namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// Defines the interface for a distributed agent request queue.
/// </summary>
/// <remarks>
/// <para>Extends <see cref="IAgentRequestQueue"/> to support distributed scenarios.</para>
/// <para>Supports priority queues, delayed tasks, dead letter queues, and more.</para>
/// </remarks>
public interface IDistributedAgentQueue : IAgentRequestQueue
{
    /// <summary>
    /// Enqueues a work item and returns a message ID.
    /// </summary>
    /// <param name="item">The work item to enqueue.</param>
    /// <param name="delay">The optional delay before execution.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The message ID.</returns>
    Task<string> EnqueueWithIdAsync(
        AgentWorkItem item,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Acknowledges that a message has been processed.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="delay">The optional delay before execution.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RequeueAsync(
        string messageId,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Moves a message to the dead letter queue.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="reason">The reason for moving the message to the dead letter queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task MoveToDeadLetterAsync(
        string messageId,
        string reason,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the number of pending messages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of pending messages.</returns>
    Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of messages in the dead letter queue.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of messages in the dead letter queue.</returns>
    Task<long> GetDeadLetterCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The consumer group name.
    /// </summary>
    string ConsumerGroup { get; }

    /// <summary>
    /// The consumer name.
    /// </summary>
    string ConsumerName { get; }
}

/// <summary>
/// Represents a distributed queue message.
/// </summary>
public record DistributedQueueMessage
{
    /// <summary>
    /// The message ID.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The work item.
    /// </summary>
    public required AgentWorkItem WorkItem { get; init; }

    /// <summary>
    /// The time when the message was enqueued.
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The retry count.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Maximum retry count
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
