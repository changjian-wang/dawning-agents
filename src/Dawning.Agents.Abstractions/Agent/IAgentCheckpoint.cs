namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 状态检查点 — 支持保存和恢复 Agent 执行上下文
/// </summary>
public interface IAgentCheckpoint
{
    /// <summary>
    /// 保存 Agent 上下文快照
    /// </summary>
    /// <param name="sessionId">会话 ID（作为检查点键）</param>
    /// <param name="context">要保存的 Agent 上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveAsync(
        string sessionId,
        AgentContext context,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 加载 Agent 上下文快照
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存的上下文，不存在时返回 null</returns>
    Task<AgentContext?> LoadAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除检查点
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查检查点是否存在
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
}
