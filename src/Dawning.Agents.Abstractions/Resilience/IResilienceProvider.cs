namespace Dawning.Agents.Abstractions.Resilience;

/// <summary>
/// Resilience strategy provider interface.
/// </summary>
/// <remarks>
/// Provides retry, circuit breaker, timeout, and other resilience strategies to protect LLM calls.
/// </remarks>
public interface IResilienceProvider
{
    /// <summary>
    /// Executes an operation with resilience strategies.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes an operation with resilience strategies (no return value).
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    );
}
