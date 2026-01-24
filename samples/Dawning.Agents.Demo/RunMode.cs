namespace Dawning.Agents.Demo;

/// <summary>
/// Demo 运行模式枚举
/// </summary>
public enum RunMode
{
    /// <summary>显示交互式菜单</summary>
    Menu,

    /// <summary>运行全部演示 (1-3)</summary>
    All,

    /// <summary>简单聊天演示</summary>
    Chat,

    /// <summary>Agent 演示</summary>
    Agent,

    /// <summary>流式聊天演示</summary>
    Stream,

    /// <summary>交互式对话</summary>
    Interactive,

    /// <summary>Memory 系统演示</summary>
    Memory,

    /// <summary>Agent + Memory 演示</summary>
    AgentMemory,

    /// <summary>包管理工具演示</summary>
    PackageManager,

    /// <summary>编排器演示</summary>
    Orchestrator,

    /// <summary>Handoff 协作演示</summary>
    Handoff,

    /// <summary>人机协作演示</summary>
    HumanLoop,

    /// <summary>可观测性演示</summary>
    Observability,

    /// <summary>扩展与部署演示</summary>
    Scaling,
}
