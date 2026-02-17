namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具只读查询接口 - 检索已注册的工具
/// </summary>
public interface IToolReader
{
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

    /// <summary>
    /// 根据分类获取工具
    /// </summary>
    /// <param name="category">工具分类</param>
    /// <returns>属于该分类的所有工具</returns>
    IReadOnlyList<ITool> GetToolsByCategory(string category);

    /// <summary>
    /// 获取所有工具分类
    /// </summary>
    /// <returns>分类名称列表</returns>
    IReadOnlyList<string> GetCategories();
}

/// <summary>
/// 工具注册接口 - 注册工具到注册表
/// </summary>
public interface IToolRegistrar
{
    /// <summary>
    /// 注册工具
    /// </summary>
    /// <param name="tool">要注册的工具</param>
    void Register(ITool tool);
}

/// <summary>
/// 工具注册表 - 管理和检索已注册的工具（组合接口，向后兼容）
/// </summary>
public interface IToolRegistry : IToolReader, IToolRegistrar { }
