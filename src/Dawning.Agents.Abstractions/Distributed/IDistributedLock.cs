namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// Defines the interface for a distributed lock.
/// </summary>
/// <remarks>
/// <para>Provides mutual exclusion in distributed environments.</para>
/// <para>Supports reentrant locks, automatic renewal, and more.</para>
/// </remarks>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// The name of the locked resource.
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// The unique identifier of the lock.
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// Gets a value indicating whether the lock has been acquired.
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// The expiration time of the lock.
    /// </summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Attempts to acquire the lock.
    /// </summary>
    /// <param name="timeout">The timeout for waiting to acquire the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the lock was acquired; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lock.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the lock duration.
    /// </summary>
    /// <param name="extension">The amount of time to extend the lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the lock was extended; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExtendAsync(TimeSpan extension, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the factory interface for creating distributed locks.
/// </summary>
public interface IDistributedLockFactory
{
    /// <summary>
    /// Creates a distributed lock.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="expiry">The lock expiration time.</param>
    /// <returns>A distributed lock instance.</returns>
    IDistributedLock CreateLock(string resource, TimeSpan expiry);

    /// <summary>
    /// Acquires a lock and executes the specified action.
    /// </summary>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="resource">The resource name.</param>
    /// <param name="expiry">The lock expiration time.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the action.</returns>
    Task<T> ExecuteWithLockAsync<T>(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Acquires a lock and executes the specified action without a return value.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="expiry">The lock expiration time.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ExecuteWithLockAsync(
        string resource,
        TimeSpan expiry,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    );
}
