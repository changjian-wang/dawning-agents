namespace Dawning.Agents.Abstractions.Orchestration;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent 编排器接口，负责协调多个 Agent 的执行
/// </summary>
/// <remarks>
/// 编排器是多 Agent 系统的核心组件，支持以下场景：
/// <list type="bullet">
/// <item>顺序执行：Agent A → Agent B → Agent C</item>
/// <item>并行执行：同时运行多个 Agent 并聚合结果</item>
/// <item>条件路由：根据结果动态选择下一个 Agent</item>
/// <item>Handoff：Agent 之间的任务交接</item>
/// </list>
/// </remarks>
public interface IOrchestrator
{
    /// <summary>
    /// 编排器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 编排器描述
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 参与编排的所有 Agent
    /// </summary>
    IReadOnlyList<IAgent> Agents { get; }

    /// <summary>
    /// 执行编排流程
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编排结果</returns>
    Task<OrchestrationResult> RunAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用上下文执行编排流程
    /// </summary>
    /// <param name="context">编排上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编排结果</returns>
    Task<OrchestrationResult> RunAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default
    );
}
