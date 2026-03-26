namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// Central message bus interface for agent communication.
/// </summary>
/// <remarks>
/// Supports the following communication patterns:
/// <list type="bullet">
/// <item>Point-to-point: Send a message to a specific agent.</item>
/// <item>Broadcast: Send a message to all agents.</item>
/// <item>Publish/Subscribe: Topic-based event notifications.</item>
/// <item>Request/Response: Synchronously wait for a response.</item>
/// </list>
/// </remarks>
public interface IMessageBus
{
    /// <summary>
    /// Sends a message to a specific agent (point-to-point).
    /// </summary>
    /// <param name="message">The message to send. <see cref="AgentMessage.ReceiverId"/> must be set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to all agents.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages for a specific agent.
    /// </summary>
    /// <param name="agentId">Subscriber agent ID.</param>
    /// <param name="handler">Message handler.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes when disposed.</returns>
    IDisposable Subscribe(string agentId, Action<AgentMessage> handler);

    /// <summary>
    /// Subscribes to events on a specific topic.
    /// </summary>
    /// <param name="agentId">Subscriber agent ID.</param>
    /// <param name="topic">Topic name.</param>
    /// <param name="handler">Event handler.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes when disposed.</returns>
    IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler);

    /// <summary>
    /// Publishes an event to a topic.
    /// </summary>
    /// <param name="topic">Topic name.</param>
    /// <param name="message">Event message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(
        string topic,
        EventMessage message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Request/Response pattern: Sends a request and waits for a response.
    /// </summary>
    /// <param name="request">Task request.</param>
    /// <param name="timeout">Response timeout duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response message.</returns>
    /// <exception cref="TimeoutException">Thrown when the response wait times out.</exception>
    Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
}
