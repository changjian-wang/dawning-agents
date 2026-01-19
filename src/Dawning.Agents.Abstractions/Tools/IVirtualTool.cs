namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 虚拟工具接口 - 延迟加载的工具组
/// </summary>
/// <remarks>
/// <para>虚拟工具是一种特殊的工具，它本身代表一组相关工具的摘要</para>
/// <para>LLM 首先看到虚拟工具的描述，当需要时再展开为具体工具</para>
/// <para>这种设计减少了 LLM 的工具选择压力，参考 GitHub Copilot 的 Virtual Tools</para>
/// </remarks>
/// <example>
/// // LLM 先看到 "FileSystemTools - 文件系统操作工具集"
/// // 当 LLM 调用此虚拟工具时，自动展开为 13 个具体文件操作工具
/// </example>
public interface IVirtualTool : ITool
{
    /// <summary>
    /// 展开后的具体工具列表
    /// </summary>
    IReadOnlyList<ITool> ExpandedTools { get; }

    /// <summary>
    /// 工具是否已展开
    /// </summary>
    bool IsExpanded { get; }

    /// <summary>
    /// 展开工具组，使具体工具可用
    /// </summary>
    void Expand();

    /// <summary>
    /// 折叠工具组，隐藏具体工具
    /// </summary>
    void Collapse();

    /// <summary>
    /// 关联的工具集
    /// </summary>
    IToolSet ToolSet { get; }
}
