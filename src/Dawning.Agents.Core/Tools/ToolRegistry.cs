using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 工具注册表实现
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IToolSet> _toolSets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IVirtualTool> _virtualTools = [];
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry>? logger = null)
    {
        _logger = logger ?? NullLogger<ToolRegistry>.Instance;
    }

    /// <summary>
    /// 注册工具
    /// </summary>
    /// <param name="tool">要注册的工具</param>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.TryGetValue(tool.Name, out var existing))
        {
            _logger.LogWarning("工具 '{Name}' 已存在，将被覆盖", tool.Name);
        }

        _tools[tool.Name] = tool;
        _logger.LogDebug("已注册工具: {Name} - {Description}", tool.Name, tool.Description);
    }

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    /// <param name="name">工具名称（不区分大小写）</param>
    /// <returns>工具实例，未找到时返回 null</returns>
    public ITool? GetTool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tools.GetValueOrDefault(name);
    }

    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    public IReadOnlyList<ITool> GetAllTools() => _tools.Values.ToList().AsReadOnly();

    /// <summary>
    /// 检查工具是否已注册
    /// </summary>
    /// <param name="name">工具名称</param>
    public bool HasTool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tools.ContainsKey(name);
    }

    /// <summary>
    /// 已注册的工具数量
    /// </summary>
    public int Count => _tools.Count;

    /// <summary>
    /// 根据分类获取工具
    /// </summary>
    /// <param name="category">工具分类</param>
    /// <returns>属于该分类的所有工具</returns>
    public IReadOnlyList<ITool> GetToolsByCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return _tools
            .Values.Where(t =>
                string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)
            )
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 获取所有工具分类
    /// </summary>
    /// <returns>分类名称列表</returns>
    public IReadOnlyList<string> GetCategories()
    {
        return _tools
            .Values.Where(t => !string.IsNullOrWhiteSpace(t.Category))
            .Select(t => t.Category!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 注册工具集
    /// </summary>
    /// <param name="toolSet">要注册的工具集</param>
    public void RegisterToolSet(IToolSet toolSet)
    {
        ArgumentNullException.ThrowIfNull(toolSet);

        if (_toolSets.TryGetValue(toolSet.Name, out var existing))
        {
            _logger.LogWarning("工具集 '{Name}' 已存在，将被覆盖", toolSet.Name);
        }

        _toolSets[toolSet.Name] = toolSet;

        // 同时注册工具集中的所有工具
        foreach (var tool in toolSet.Tools)
        {
            Register(tool);
        }

        _logger.LogDebug(
            "已注册工具集: {Name} - {Description} ({Count} 个工具)",
            toolSet.Name,
            toolSet.Description,
            toolSet.Count
        );
    }

    /// <summary>
    /// 根据名称获取工具集
    /// </summary>
    /// <param name="name">工具集名称</param>
    /// <returns>工具集实例，未找到时返回 null</returns>
    public IToolSet? GetToolSet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _toolSets.GetValueOrDefault(name);
    }

    /// <summary>
    /// 获取所有已注册的工具集
    /// </summary>
    public IReadOnlyList<IToolSet> GetAllToolSets() => _toolSets.Values.ToList().AsReadOnly();

    /// <summary>
    /// 注册虚拟工具
    /// </summary>
    /// <param name="virtualTool">要注册的虚拟工具</param>
    public void RegisterVirtualTool(IVirtualTool virtualTool)
    {
        ArgumentNullException.ThrowIfNull(virtualTool);

        // 注册虚拟工具本身
        Register(virtualTool);
        _virtualTools.Add(virtualTool);

        // 注册关联的工具集
        RegisterToolSet(virtualTool.ToolSet);

        _logger.LogDebug(
            "已注册虚拟工具: {Name} (展开后 {Count} 个工具)",
            virtualTool.Name,
            virtualTool.ExpandedTools.Count
        );
    }

    /// <summary>
    /// 获取所有虚拟工具
    /// </summary>
    public IReadOnlyList<IVirtualTool> GetVirtualTools() => _virtualTools.AsReadOnly();

    /// <summary>
    /// 从类型扫描并注册所有 [FunctionTool] 标记的方法
    /// </summary>
    /// <typeparam name="T">工具类类型</typeparam>
    public void RegisterToolsFromType<T>()
        where T : class, new()
    {
        var scanner = new ToolScanner();
        var instance = new T();
        foreach (var tool in scanner.ScanInstance(instance))
        {
            Register(tool);
        }
    }

    /// <summary>
    /// 从实例扫描并注册所有 [FunctionTool] 标记的方法
    /// </summary>
    /// <param name="instance">工具类实例</param>
    public void RegisterToolsFromInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var scanner = new ToolScanner();
        foreach (var tool in scanner.ScanInstance(instance))
        {
            Register(tool);
        }
    }
}
