namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// Shared state storage interface for multi-agent collaboration.
/// </summary>
/// <remarks>
/// Shared state enables data exchange between agents and supports:
/// <list type="bullet">
/// <item>Key-value storage.</item>
/// <item>Pattern-matching queries.</item>
/// <item>Change notifications.</item>
/// </list>
/// </remarks>
public interface ISharedState
{
    /// <summary>
    /// Gets a value from the shared state.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The key name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value, or <see langword="null"/> if the key does not exist.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the shared state.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The key name.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a value from the shared state.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the value was successfully deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified key exists.
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all keys matching the specified pattern.
    /// </summary>
    /// <param name="pattern">The match pattern (supports * wildcard).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching keys.</returns>
    Task<IReadOnlyList<string>> GetKeysAsync(
        string pattern = "*",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Subscribes to change notifications for a key.
    /// </summary>
    /// <param name="key">The key to monitor.</param>
    /// <param name="handler">Change handler.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes when disposed.</returns>
    IDisposable OnChange(string key, Action<string, object?> handler);

    /// <summary>
    /// Clears all shared state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of key-value pairs currently stored.
    /// </summary>
    int Count { get; }
}
