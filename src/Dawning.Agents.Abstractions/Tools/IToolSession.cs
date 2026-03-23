namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具会话 — 管理 session 级别的动态工具，聚合多层级工具解析
/// </summary>
/// <remarks>
/// <para>工具解析顺序: Core → Session → User → Global → MCP</para>
/// <para>Session 工具存储在内存中，随会话销毁</para>
/// </remarks>
public interface IToolSession : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 创建并注册一个动态脚本工具到当前 session
    /// </summary>
    /// <param name="definition">工具定义</param>
    /// <returns>创建的工具实例</returns>
    ITool CreateTool(EphemeralToolDefinition definition);

    /// <summary>
    /// 获取当前 session 的所有动态工具
    /// </summary>
    IReadOnlyList<ITool> GetSessionTools();

    /// <summary>
    /// 提升工具的持久化层级（Session → User 或 Global）
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="targetScope">目标范围</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task PromoteToolAsync(
        string name,
        ToolScope targetScope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 从指定层级移除工具
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="scope">工具范围</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveToolAsync(
        string name,
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 列出指定层级的所有工具定义
    /// </summary>
    /// <param name="scope">工具范围</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<EphemeralToolDefinition>> ListToolsAsync(
        ToolScope scope,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 更新 session 工具的定义（原地修订，用于反思修复）
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="definition">修订后的工具定义</param>
    /// <returns>更新后的工具实例</returns>
    ITool UpdateTool(string name, EphemeralToolDefinition definition);
}
