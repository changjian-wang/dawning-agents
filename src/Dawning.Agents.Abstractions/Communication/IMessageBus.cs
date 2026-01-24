namespace Dawning.Agents.Abstractions.Communication;

/// <summary>
/// Agent 通信的中央消息总线接口
/// </summary>
/// <remarks>
/// 支持以下通信模式：
/// <list type="bullet">
/// <item>点对点：向特定 Agent 发送消息</item>
/// <item>广播：向所有 Agent 发送消息</item>
/// <item>发布/订阅：基于主题的事件通知</item>
/// <item>请求/响应：同步等待响应</item>
/// </list>
/// </remarks>
public interface IMessageBus
{
    /// <summary>
    /// 向特定 Agent 发送消息（点对点）
    /// </summary>
    /// <param name="message">要发送的消息（必须设置 ReceiverId）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向所有 Agent 广播消息
    /// </summary>
    /// <param name="message">要广播的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task BroadcastAsync(AgentMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅指定 Agent 的消息
    /// </summary>
    /// <param name="agentId">订阅者 Agent ID</param>
    /// <param name="handler">消息处理器</param>
    /// <returns>取消订阅的 Disposable</returns>
    IDisposable Subscribe(string agentId, Action<AgentMessage> handler);

    /// <summary>
    /// 订阅指定主题的事件
    /// </summary>
    /// <param name="agentId">订阅者 Agent ID</param>
    /// <param name="topic">主题名称</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>取消订阅的 Disposable</returns>
    IDisposable Subscribe(string agentId, string topic, Action<EventMessage> handler);

    /// <summary>
    /// 向主题发布事件
    /// </summary>
    /// <param name="topic">主题名称</param>
    /// <param name="message">事件消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PublishAsync(string topic, EventMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 请求/响应模式：发送请求并等待响应
    /// </summary>
    /// <param name="request">任务请求</param>
    /// <param name="timeout">响应超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应消息</returns>
    /// <exception cref="TimeoutException">等待响应超时</exception>
    Task<ResponseMessage> RequestAsync(
        TaskMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
}
