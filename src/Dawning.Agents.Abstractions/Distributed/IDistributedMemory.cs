using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Abstractions.Distributed;

/// <summary>
/// Defines the interface for distributed memory.
/// </summary>
/// <remarks>
/// <para>Extends <see cref="IConversationMemory"/> to support distributed scenarios.</para>
/// <para>Provides session locking and cross-node synchronization capabilities.</para>
/// </remarks>
public interface IDistributedMemory : IConversationMemory
{
    /// <summary>
    /// The session ID (always non-null for distributed memory).
    /// </summary>
    new string SessionId { get; }

    /// <summary>
    /// Attempts to acquire a session lock.
    /// </summary>
    /// <param name="timeout">The lock timeout duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the lock was acquired; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryLockSessionAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the session lock.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task UnlockSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the session expiration time.
    /// </summary>
    /// <param name="expiry">The expiration duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SetExpiryAsync(TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the session expiration time.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RefreshExpiryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the session exists.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the session exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}
