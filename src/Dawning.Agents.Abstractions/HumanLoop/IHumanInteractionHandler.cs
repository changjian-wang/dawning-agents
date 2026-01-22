namespace Dawning.Agents.Abstractions.HumanLoop;

/// <summary>
/// 通知级别
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// 信息
    /// </summary>
    Info,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 成功
    /// </summary>
    Success,
}

/// <summary>
/// 人机交互接口
/// </summary>
public interface IHumanInteractionHandler
{
    /// <summary>
    /// 请求人工确认
    /// </summary>
    /// <param name="request">确认请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>确认响应</returns>
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 请求人工输入/反馈
    /// </summary>
    /// <param name="prompt">提示信息</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户输入</returns>
    Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 通知人类（无需响应）
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="level">通知级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 升级到人工处理
    /// </summary>
    /// <param name="request">升级请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>升级结果</returns>
    Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    );
}
