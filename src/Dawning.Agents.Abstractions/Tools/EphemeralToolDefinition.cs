namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// 动态脚本工具定义 — 描述一个可在运行时创建的脚本工具
/// </summary>
public class EphemeralToolDefinition
{
    /// <summary>
    /// 工具名称（snake_case，唯一标识符）
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 工具用途描述（供 LLM 理解）
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// 脚本运行时
    /// </summary>
    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.Bash;

    /// <summary>
    /// 脚本内容，用 $param_name 引用参数
    /// </summary>
    public required string Script { get; set; }

    /// <summary>
    /// 工具参数列表
    /// </summary>
    public List<ScriptParameter> Parameters { get; set; } = [];

    /// <summary>
    /// 持久化范围
    /// </summary>
    public ToolScope Scope { get; set; } = ToolScope.Session;

    /// <summary>
    /// 元数据
    /// </summary>
    public EphemeralToolMetadata Metadata { get; set; } = new();
}

/// <summary>
/// 脚本参数定义
/// </summary>
public record ScriptParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 参数类型（string, int, bool）
    /// </summary>
    public string Type { get; init; } = "string";

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// 动态工具元数据
/// </summary>
public class EphemeralToolMetadata
{
    /// <summary>
    /// 创建者
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// 脚本运行时类型
/// </summary>
public enum ScriptRuntime
{
    /// <summary>
    /// Bash / sh
    /// </summary>
    Bash,
}

/// <summary>
/// 工具持久化范围
/// </summary>
public enum ToolScope
{
    /// <summary>
    /// Session 级别 — 仅在当前会话中存活（内存）
    /// </summary>
    Session,

    /// <summary>
    /// User 级别 — 跨项目持久化 (~/.dawning/tools/)
    /// </summary>
    User,

    /// <summary>
    /// Global 级别 — 项目级持久化 ({project}/.dawning/tools/)，可提交 git
    /// </summary>
    Global,
}
