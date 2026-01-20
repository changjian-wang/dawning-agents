namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Memory 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Memory": {
///     "Type": "Window",
///     "WindowSize": 10,
///     "MaxRecentMessages": 6,
///     "SummaryThreshold": 10,
///     "ModelName": "gpt-4",
///     "MaxContextTokens": 8192
///   }
/// }
/// </code>
/// </remarks>
public class MemoryOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Memory";

    /// <summary>
    /// Memory 类型：Buffer、Window、Summary
    /// </summary>
    public MemoryType Type { get; set; } = MemoryType.Buffer;

    /// <summary>
    /// 窗口大小（WindowMemory 使用）
    /// </summary>
    public int WindowSize { get; set; } = 10;

    /// <summary>
    /// 保留的最近消息数（SummaryMemory 使用）
    /// </summary>
    public int MaxRecentMessages { get; set; } = 6;

    /// <summary>
    /// 触发摘要的消息数阈值（SummaryMemory 使用）
    /// </summary>
    public int SummaryThreshold { get; set; } = 10;

    /// <summary>
    /// Token 计数器的模型名称
    /// </summary>
    public string ModelName { get; set; } = "gpt-4";

    /// <summary>
    /// 最大上下文 token 数
    /// </summary>
    public int MaxContextTokens { get; set; } = 8192;
}

/// <summary>
/// Memory 类型枚举
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// 缓冲记忆 - 存储所有消息
    /// </summary>
    Buffer,

    /// <summary>
    /// 窗口记忆 - 只保留最后 N 条消息
    /// </summary>
    Window,

    /// <summary>
    /// 摘要记忆 - 自动摘要旧消息
    /// </summary>
    Summary,
}
