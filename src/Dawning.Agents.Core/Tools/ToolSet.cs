using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 工具集实现 - 将相关工具分组管理
/// </summary>
public class ToolSet : IToolSet
{
    private readonly Dictionary<string, ITool> _toolsByName;

    /// <summary>
    /// 工具集名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 工具集描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 工具集图标
    /// </summary>
    public string? Icon { get; }

    /// <summary>
    /// 工具集包含的所有工具
    /// </summary>
    public IReadOnlyList<ITool> Tools { get; }

    /// <summary>
    /// 工具数量
    /// </summary>
    public int Count => Tools.Count;

    /// <summary>
    /// 创建工具集
    /// </summary>
    /// <param name="name">工具集名称</param>
    /// <param name="description">工具集描述</param>
    /// <param name="tools">包含的工具</param>
    /// <param name="icon">图标（可选）</param>
    public ToolSet(string name, string description, IEnumerable<ITool> tools, string? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentNullException.ThrowIfNull(tools, nameof(tools));

        Name = name;
        Description = description;
        Icon = icon;
        Tools = tools.ToList().AsReadOnly();
        _toolsByName = Tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    public ITool? GetTool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));
        return _toolsByName.GetValueOrDefault(toolName);
    }

    /// <summary>
    /// 检查是否包含指定工具
    /// </summary>
    public bool Contains(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName, nameof(toolName));
        return _toolsByName.ContainsKey(toolName);
    }

    /// <summary>
    /// 从工具类型创建工具集
    /// </summary>
    /// <typeparam name="T">工具类型</typeparam>
    /// <param name="name">工具集名称</param>
    /// <param name="description">工具集描述</param>
    /// <param name="icon">图标（可选）</param>
    /// <returns>工具集实例</returns>
    public static ToolSet FromType<T>(string name, string description, string? icon = null)
        where T : class, new()
    {
        var registry = new ToolRegistry();
        registry.RegisterToolsFromType<T>();
        return new ToolSet(name, description, registry.GetAllTools(), icon);
    }
}
