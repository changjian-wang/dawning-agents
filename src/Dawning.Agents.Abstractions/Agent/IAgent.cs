namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 核心接口
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Agent 名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Agent 系统指令
    /// </summary>
    string Instructions { get; }

    /// <summary>
    /// 执行 Agent 任务
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 响应</returns>
    Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定上下文执行 Agent 任务
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 响应</returns>
    Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    );
}
