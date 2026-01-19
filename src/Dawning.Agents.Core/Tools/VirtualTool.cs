using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 虚拟工具实现 - 延迟加载的工具组
/// </summary>
/// <remarks>
/// <para>虚拟工具在未展开时作为一个摘要工具出现</para>
/// <para>当 LLM 调用它时，自动展开为具体工具列表</para>
/// </remarks>
public class VirtualTool : IVirtualTool
{
    private bool _isExpanded;

    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 工具描述（虚拟工具的摘要描述）
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 参数 Schema（虚拟工具不需要参数，只需调用即可展开）
    /// </summary>
    public string ParametersSchema => """{"type": "object", "properties": {}}""";

    /// <summary>
    /// 是否需要确认
    /// </summary>
    public bool RequiresConfirmation => false;

    /// <summary>
    /// 风险等级（虚拟工具本身是低风险的，展开后的工具各有风险等级）
    /// </summary>
    public ToolRiskLevel RiskLevel => ToolRiskLevel.Low;

    /// <summary>
    /// 工具分类
    /// </summary>
    public string? Category => "VirtualTool";

    /// <summary>
    /// 关联的工具集
    /// </summary>
    public IToolSet ToolSet { get; }

    /// <summary>
    /// 展开后的具体工具列表
    /// </summary>
    public IReadOnlyList<ITool> ExpandedTools => ToolSet.Tools;

    /// <summary>
    /// 是否已展开
    /// </summary>
    public bool IsExpanded => _isExpanded;

    /// <summary>
    /// 创建虚拟工具
    /// </summary>
    /// <param name="toolSet">关联的工具集</param>
    public VirtualTool(IToolSet toolSet)
    {
        ArgumentNullException.ThrowIfNull(toolSet, nameof(toolSet));

        ToolSet = toolSet;
        Name = toolSet.Name;
        Description = BuildDescription(toolSet);
    }

    /// <summary>
    /// 创建虚拟工具（自定义名称和描述）
    /// </summary>
    /// <param name="name">虚拟工具名称</param>
    /// <param name="description">虚拟工具描述</param>
    /// <param name="toolSet">关联的工具集</param>
    public VirtualTool(string name, string description, IToolSet toolSet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentNullException.ThrowIfNull(toolSet, nameof(toolSet));

        Name = name;
        Description = description;
        ToolSet = toolSet;
    }

    /// <summary>
    /// 展开工具组
    /// </summary>
    public void Expand() => _isExpanded = true;

    /// <summary>
    /// 折叠工具组
    /// </summary>
    public void Collapse() => _isExpanded = false;

    /// <summary>
    /// 执行虚拟工具（返回工具列表信息，提示 LLM 使用具体工具）
    /// </summary>
    public Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        Expand();

        var toolNames = string.Join(", ", ToolSet.Tools.Select(t => t.Name));
        var output =
            $"工具组 '{ToolSet.Name}' 已展开，包含以下 {ToolSet.Count} 个工具：{toolNames}。请使用具体的工具名称来执行操作。";

        return Task.FromResult(ToolResult.Ok(output));
    }

    /// <summary>
    /// 从工具集创建虚拟工具
    /// </summary>
    public static VirtualTool FromToolSet(IToolSet toolSet) => new(toolSet);

    /// <summary>
    /// 从工具类型创建虚拟工具
    /// </summary>
    public static VirtualTool FromType<T>(string name, string description, string? icon = null)
        where T : class, new()
    {
        var newToolSet = Tools.ToolSet.FromType<T>(name, description, icon);
        return new VirtualTool(newToolSet);
    }

    private static string BuildDescription(IToolSet toolSet)
    {
        var toolSummary = string.Join(", ", toolSet.Tools.Take(5).Select(t => t.Name));

        if (toolSet.Tools.Count > 5)
        {
            toolSummary += $" 等 {toolSet.Tools.Count} 个工具";
        }

        return $"{toolSet.Description}（包含: {toolSummary}）";
    }
}
