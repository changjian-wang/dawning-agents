namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具选择器接口 - 智能选择与查询相关的工具
/// </summary>
/// <remarks>
/// <para>当工具数量过多时，工具选择器可以根据用户查询智能筛选最相关的工具</para>
/// <para>参考 GitHub Copilot 的 Embedding-Guided Tool Routing</para>
/// </remarks>
public interface IToolSelector
{
    /// <summary>
    /// 根据查询选择最相关的工具
    /// </summary>
    /// <param name="query">用户查询或任务描述</param>
    /// <param name="availableTools">可用的工具列表</param>
    /// <param name="maxTools">返回的最大工具数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相关性排序的工具列表</returns>
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query,
        IReadOnlyList<ITool> availableTools,
        int maxTools = 20,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 根据查询选择最相关的工具集
    /// </summary>
    /// <param name="query">用户查询或任务描述</param>
    /// <param name="availableToolSets">可用的工具集列表</param>
    /// <param name="maxToolSets">返回的最大工具集数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>按相关性排序的工具集列表</returns>
    Task<IReadOnlyList<IToolSet>> SelectToolSetsAsync(
        string query,
        IReadOnlyList<IToolSet> availableToolSets,
        int maxToolSets = 5,
        CancellationToken cancellationToken = default
    );
}
