namespace Dawning.Agents.MCP.Transport;

/// <summary>
/// MCP 传输层接口
/// </summary>
public interface IMCPTransport : IAsyncDisposable
{
    /// <summary>
    /// 启动传输
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送消息
    /// </summary>
    Task SendAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 接收消息
    /// </summary>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }
}
