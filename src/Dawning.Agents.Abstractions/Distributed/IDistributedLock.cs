namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// 分布式锁接口
/// </summary>
/// <remarks>
/// <para>用于在分布式环境中实现互斥访问</para>
/// <para>支持可重入锁、自动续期等特性</para>
/// </remarks>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// 锁的资源名称
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// 锁的唯一标识
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 是否已获取锁
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// 锁的过期时间
    /// </summary>
    DateTime? ExpiresAt { get; }

    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="timeout">等待超时</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功获取锁</returns>
    Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 延长锁的持有时间
    /// </summary>
    /// <param name="extension">延长时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功延长</returns>
    Task<bool> ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default);
}

/// <summary>
/// 分布式锁工厂接口
/// </summary>
public interface IDistributedLockFactory
{
    /// <summary>
    /// 创建分布式锁
    /// </summary>
    /// <param name="resource">资源名称</param>
    /// <param name="expiry">锁过期时间</param>
    /// <returns>分布式锁实例</returns>
    IDistributedLock CreateLock(string resource, TimeSpan expiry);

    /// <summary>
    /// 获取锁并执行操作
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="resource">资源名称</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> ExecuteWithLockAsync<T>(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 获取锁并执行操作（无返回值）
    /// </summary>
    /// <param name="resource">资源名称</param>
    /// <param name="expiry">锁过期时间</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    );
}
