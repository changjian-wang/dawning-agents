using Dawning.Agents.Abstractions.Agent;

namespace Dawning.Agents.Abstractions.Handoff;

/// <summary>
/// Handoff Agent 接口 - 支持 Handoff 能力的 Agent
/// </summary>
/// <remarks>
/// 继承自 IAgent，扩展了 Handoff 相关能力：
/// - 声明可以转交的目标 Agent 列表
/// - 在执行过程中可以请求 Handoff
/// </remarks>
public interface IHandoffAgent : IAgent
{
    /// <summary>
    /// 此 Agent 可以转交的目标 Agent 名称列表
    /// </summary>
    /// <remarks>
    /// 如果为空，表示此 Agent 不会主动发起 Handoff
    /// </remarks>
    IReadOnlyList<string> Handoffs { get; }
}

/// <summary>
/// Handoff 处理器接口 - 负责管理和执行 Handoff
/// </summary>
public interface IHandoffHandler
{
    /// <summary>
    /// 注册 Agent 到 Handoff 系统
    /// </summary>
    /// <param name="agent">要注册的 Agent</param>
    void RegisterAgent(IAgent agent);

    /// <summary>
    /// 批量注册 Agent
    /// </summary>
    /// <param name="agents">要注册的 Agent 集合</param>
    void RegisterAgents(IEnumerable<IAgent> agents);

    /// <summary>
    /// 获取已注册的 Agent
    /// </summary>
    /// <param name="name">Agent 名称</param>
    /// <returns>Agent 实例，如果未找到则返回 null</returns>
    IAgent? GetAgent(string name);

    /// <summary>
    /// 获取所有已注册的 Agent
    /// </summary>
    IReadOnlyList<IAgent> GetAllAgents();

    /// <summary>
    /// 执行 Handoff 请求
    /// </summary>
    /// <param name="request">Handoff 请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Handoff 执行结果</returns>
    Task<HandoffResult> ExecuteHandoffAsync(
        HandoffRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 从指定的入口 Agent 开始执行任务，支持自动 Handoff
    /// </summary>
    /// <param name="entryAgentName">入口 Agent 名称</param>
    /// <param name="input">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最终执行结果</returns>
    Task<HandoffResult> RunWithHandoffAsync(
        string entryAgentName,
        string input,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Agent 响应扩展 - 用于检测 Handoff 请求
/// </summary>
public static class AgentResponseHandoffExtensions
{
    /// <summary>
    /// Handoff 标记前缀
    /// </summary>
    public const string HandoffPrefix = "[HANDOFF:";

    /// <summary>
    /// 检查响应是否包含 Handoff 请求
    /// </summary>
    public static bool IsHandoffRequest(this AgentResponse response)
    {
        return response.Success
            && !string.IsNullOrEmpty(response.FinalAnswer)
            && response.FinalAnswer.StartsWith(HandoffPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从响应中解析 Handoff 请求
    /// </summary>
    /// <returns>解析的 Handoff 请求，如果不是 Handoff 响应则返回 null</returns>
    public static HandoffRequest? ParseHandoffRequest(this AgentResponse response)
    {
        if (!response.IsHandoffRequest())
        {
            return null;
        }

        var answer = response.FinalAnswer!;

        // 格式: [HANDOFF:TargetAgent] Input text
        // 或: [HANDOFF:TargetAgent|Reason] Input text
        var endBracket = answer.IndexOf(']');
        if (endBracket < 0)
        {
            return null;
        }

        var handoffPart = answer[HandoffPrefix.Length..endBracket];
        var input = answer[(endBracket + 1)..].Trim();

        string targetAgent;
        string? reason = null;

        var pipeIndex = handoffPart.IndexOf('|');
        if (pipeIndex > 0)
        {
            targetAgent = handoffPart[..pipeIndex].Trim();
            reason = handoffPart[(pipeIndex + 1)..].Trim();
        }
        else
        {
            targetAgent = handoffPart.Trim();
        }

        if (string.IsNullOrEmpty(targetAgent))
        {
            return null;
        }

        return new HandoffRequest
        {
            TargetAgentName = targetAgent,
            Input = string.IsNullOrEmpty(input) ? response.FinalAnswer! : input,
            Reason = reason,
        };
    }

    /// <summary>
    /// 创建 Handoff 响应
    /// </summary>
    /// <param name="targetAgent">目标 Agent 名称</param>
    /// <param name="input">传递给目标 Agent 的输入</param>
    /// <param name="reason">转交原因</param>
    /// <returns>格式化的 Handoff 响应字符串</returns>
    public static string CreateHandoffResponse(
        string targetAgent,
        string input,
        string? reason = null
    )
    {
        if (string.IsNullOrEmpty(reason))
        {
            return $"{HandoffPrefix}{targetAgent}] {input}";
        }

        return $"{HandoffPrefix}{targetAgent}|{reason}] {input}";
    }
}
