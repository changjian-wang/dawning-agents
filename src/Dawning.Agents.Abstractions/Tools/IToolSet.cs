namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 工具集接口 - 将相关工具分组管理
/// </summary>
/// <remarks>
/// <para>工具集允许将功能相关的工具组织在一起，便于管理和引用</para>
/// <para>参考 GitHub Copilot 的 Tool Sets 设计</para>
/// </remarks>
/// <example>
/// var searchTools = new ToolSet("search", "搜索相关工具",
///     [grepTool, searchFilesTool, semanticSearchTool]);
/// </example>
public interface IToolSet
{
    /// <summary>
    /// 工具集名称（唯一标识符）
    /// </summary>
    /// <example>search, edit, filesystem</example>
    string Name { get; }

    /// <summary>
    /// 工具集描述（供 LLM 理解工具集用途）
    /// </summary>
    /// <example>搜索相关工具，包括文件搜索、代码搜索等</example>
    string Description { get; }

    /// <summary>
    /// 工具集图标（可选，用于 UI 显示）
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// 工具集包含的所有工具
    /// </summary>
    IReadOnlyList<ITool> Tools { get; }

    /// <summary>
    /// 工具数量
    /// </summary>
    int Count => Tools.Count;

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <returns>工具实例，不存在时返回 null</returns>
    ITool? GetTool(string toolName);

    /// <summary>
    /// 检查工具集是否包含指定工具
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <returns>是否包含</returns>
    bool Contains(string toolName);
}
