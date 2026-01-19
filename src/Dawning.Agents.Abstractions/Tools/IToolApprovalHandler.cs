namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具审批处理器接口 - 处理高风险工具的执行确认
/// </summary>
/// <remarks>
/// <para>对于需要确认的工具（RequiresConfirmation = true），在执行前需要通过审批</para>
/// <para>不同的实现可以支持不同的审批策略：自动、交互式、基于风险等级等</para>
/// </remarks>
public interface IToolApprovalHandler
{
    /// <summary>
    /// 请求工具执行批准
    /// </summary>
    /// <param name="tool">要执行的工具</param>
    /// <param name="input">工具输入参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否批准执行</returns>
    Task<bool> RequestApprovalAsync(
        ITool tool,
        string input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 请求 URL 访问批准（用于网络请求工具）
    /// </summary>
    /// <param name="tool">发起请求的工具</param>
    /// <param name="url">要访问的 URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否批准访问</returns>
    Task<bool> RequestUrlApprovalAsync(
        ITool tool,
        string url,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 请求终端命令执行批准
    /// </summary>
    /// <param name="tool">发起请求的工具</param>
    /// <param name="command">要执行的命令</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否批准执行</returns>
    Task<bool> RequestCommandApprovalAsync(
        ITool tool,
        string command,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// 审批策略枚举
/// </summary>
public enum ApprovalStrategy
{
    /// <summary>
    /// 总是自动批准
    /// </summary>
    AlwaysApprove,

    /// <summary>
    /// 总是拒绝（只读模式）
    /// </summary>
    AlwaysDeny,

    /// <summary>
    /// 基于风险等级决定（Low 自动批准，Medium/High 需要确认）
    /// </summary>
    RiskBased,

    /// <summary>
    /// 总是需要交互式确认
    /// </summary>
    Interactive,
}
