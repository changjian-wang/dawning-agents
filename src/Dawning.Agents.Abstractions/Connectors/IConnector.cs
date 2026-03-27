namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Base connector interface — unified lifecycle for external system integrations.
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Connector display name (e.g., "MicrosoftGraphEmail", "Notion").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the connector is currently connected and operational.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Establishes a connection to the external system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the external system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
