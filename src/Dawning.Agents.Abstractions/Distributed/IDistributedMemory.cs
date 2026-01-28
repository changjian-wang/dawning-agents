using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// 分布式记忆接口
/// </summary>
/// <remarks>
/// <para>扩展 <see cref="IConversationMemory"/> 以支持分布式场景</para>
/// <para>提供会话锁定、跨节点同步等能力</para>
/// </remarks>
public interface IDistributedMemory : IConversationMemory
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// 尝试获取会话锁
    /// </summary>
    /// <param name="timeout">锁超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功获取锁</returns>
    Task<bool> TryLockSessionAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放会话锁
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task UnlockSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置会话过期时间
    /// </summary>
    /// <param name="expiry">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetExpiryAsync(TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刷新会话过期时间
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task RefreshExpiryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查会话是否存在
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话是否存在</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
