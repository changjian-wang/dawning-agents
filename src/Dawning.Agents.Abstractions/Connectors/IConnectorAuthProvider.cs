namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Connector authentication provider — abstracts OAuth2 / API key / token credential flows.
/// </summary>
public interface IConnectorAuthProvider
{
    /// <summary>
    /// Gets a valid access token for the external system.
    /// </summary>
    /// <param name="scopes">Required permission scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid access token string.</returns>
    Task<string> GetAccessTokenAsync(
        IEnumerable<string>? scopes = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Whether the current credentials are valid and not expired.
    /// </summary>
    bool IsAuthenticated { get; }
}
