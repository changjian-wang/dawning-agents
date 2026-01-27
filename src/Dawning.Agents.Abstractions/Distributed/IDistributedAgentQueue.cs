using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Scaling;

namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// 分布式 Agent 请求队列接口
/// </summary>
/// <remarks>
/// <para>扩展 <see cref="IAgentRequestQueue"/> 以支持分布式场景</para>
/// <para>支持优先级队列、延迟任务、死信队列等特性</para>
/// </remarks>
public interface IDistributedAgentQueue : IAgentRequestQueue
{
    /// <summary>
    /// 入队工作项（带消息 ID）
    /// </summary>
    /// <param name="item">工作项</param>
    /// <param name="delay">延迟执行时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息 ID</returns>
    Task<string> EnqueueWithIdAsync(
        AgentWorkItem item,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 确认消息处理完成
    /// </summary>
    /// <param name="messageId">消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将消息重新入队
    /// </summary>
    /// <param name="messageId">消息 ID</param>
    /// <param name="delay">延迟执行时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RequeueAsync(
        string messageId,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 将消息移入死信队列
    /// </summary>
    /// <param name="messageId">消息 ID</param>
    /// <param name="reason">移入死信队列的原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task MoveToDeadLetterAsync(
        string messageId,
        string reason,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取待处理消息数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待处理消息数量</returns>
    Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取死信队列消息数量
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>死信队列消息数量</returns>
    Task<long> GetDeadLetterCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 消费者组名称
    /// </summary>
    string ConsumerGroup { get; }

    /// <summary>
    /// 消费者名称
    /// </summary>
    string ConsumerName { get; }
}

/// <summary>
/// 分布式队列消息
/// </summary>
public record DistributedQueueMessage
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// 工作项
    /// </summary>
    public required AgentWorkItem WorkItem { get; init; }

    /// <summary>
    /// 入队时间
    /// </summary>
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
