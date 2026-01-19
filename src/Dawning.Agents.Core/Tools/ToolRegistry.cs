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
}
