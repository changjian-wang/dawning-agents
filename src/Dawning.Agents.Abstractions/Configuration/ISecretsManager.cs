namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// Secrets management interface.
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// Gets a secret.
    /// </summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret value, or <see langword="null"/> if it does not exist.</returns>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a secret.
    /// </summary>
    /// <param name="name">Secret name.</param>
    /// <param name="value">Secret value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a secret exists.
    /// </summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}
