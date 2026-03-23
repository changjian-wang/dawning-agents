namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具持久化存储 — 管理 User 和 Global 级别的工具定义
/// </summary>
/// <remarks>
/// <para>User 工具存储在 ~/.dawning/tools/ 目录</para>
/// <para>Global 工具存储在 {project}/.dawning/tools/ 目录</para>
/// </remarks>
public interface IToolStore
{
    /// <summary>
    /// 加载指定范围的所有工具定义
    /// </summary>
    /// <param name="scope">工具范围（User 或 Global）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具定义列表</returns>
    Task<IReadOnlyList<EphemeralToolDefinition>> LoadToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 保存工具定义到指定范围
    /// </summary>
    /// <param name="definition">工具定义</param>
    /// <param name="scope">工具范围（User 或 Global）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 从指定范围删除工具定义
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="scope">工具范围（User 或 Global）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 检查工具是否存在于指定范围
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="scope">工具范围</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 更新工具定义（自动递增版本号和修订时间）
    /// </summary>
    /// <param name="definition">修订后的工具定义</param>
    /// <param name="scope">工具范围（User 或 Global）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateToolAsync(
        EphemeralToolDefinition definition,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );
}
