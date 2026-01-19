namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具注册表 - 管理和检索已注册的工具
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 注册工具
    /// </summary>
    /// <param name="tool">要注册的工具</param>
    void Register(ITool tool);

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    /// <param name="name">工具名称（不区分大小写）</param>
    /// <returns>工具实例，未找到时返回 null</returns>
    ITool? GetTool(string name);

    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    IReadOnlyList<ITool> GetAllTools();

    /// <summary>
    /// 检查工具是否已注册
    /// </summary>
    /// <param name="name">工具名称</param>
    bool HasTool(string name);

    /// <summary>
    /// 已注册的工具数量
    /// </summary>
    int Count { get; }
}
