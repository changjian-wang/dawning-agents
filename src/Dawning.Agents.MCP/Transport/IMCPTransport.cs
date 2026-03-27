namespace Dawning.Agents.MCP.Transport;

/// <summary>
/// Defines the MCP transport layer interface.
/// </summary>
public interface IMCPTransport : IAsyncDisposable
{
    /// <summary>
    /// Starts the transport.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message.
    /// </summary>
    Task SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a message.
    /// </summary>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the transport is connected.
    /// </summary>
    bool IsConnected { get; }
}
