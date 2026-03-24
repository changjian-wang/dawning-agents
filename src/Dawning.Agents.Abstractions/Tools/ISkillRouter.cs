namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 带语义匹配分数的工具
/// </summary>
/// <param name="Tool">匹配的工具</param>
/// <param name="Score">语义相似度分数 (0-1)</param>
public record ScoredTool(ITool Tool, float Score);

/// <summary>
/// 语义技能路由器 — 根据任务描述检索最相关的工具
/// </summary>
/// <remarks>
/// <para>使用语义嵌入在工具描述上建立向量索引</para>
/// <para>当工具数量超过阈值时，替代全量注入 prompt</para>
/// </remarks>
public interface ISkillRouter
{
    /// <summary>
    /// 根据任务描述语义匹配最相关的工具
    /// </summary>
    /// <param name="taskDescription">用户任务描述</param>
    /// <param name="topK">返回的最大工具数</param>
    /// <param name="minScore">最小相似度阈值 (0-1)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>带分数的工具列表，按相关性降序</returns>
    Task<IReadOnlyList<ScoredTool>> RouteAsync(
        string taskDescription,
        int topK = 5,
        float minScore = 0.3f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 重建工具索引（工具注册/删除后调用）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}
