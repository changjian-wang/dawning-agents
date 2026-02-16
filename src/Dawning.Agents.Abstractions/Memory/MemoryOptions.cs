using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Memory 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Memory": {
///     "Type": "Vector",
///     "WindowSize": 10,
///     "MaxRecentMessages": 6,
///     "SummaryThreshold": 10,
///     "DowngradeThreshold": 4000,
///     "RetrieveTopK": 5,
///     "MinRelevanceScore": 0.5,
///     "ModelName": "gpt-4",
///     "MaxContextTokens": 8192
///   }
/// }
/// </code>
/// </remarks>
public class MemoryOptions : IValidatableOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Memory";

    /// <summary>
    /// Memory 类型：Buffer、Window、Summary、Adaptive、Vector
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
    /// 触发降级的 token 阈值（AdaptiveMemory 使用）
    /// </summary>
    /// <remarks>
    /// 当 BufferMemory 中的 token 数量超过此阈值时，自动降级到 SummaryMemory
    /// </remarks>
    public int DowngradeThreshold { get; set; } = 4000;

    /// <summary>
    /// 检索的相关消息数（VectorMemory 使用）
    /// </summary>
    public int RetrieveTopK { get; set; } = 5;

    /// <summary>
    /// 最小相关性分数（VectorMemory 使用，0-1）
    /// </summary>
    public float MinRelevanceScore { get; set; } = 0.5f;

    /// <summary>
    /// Token 计数器的模型名称
    /// </summary>
    public string ModelName { get; set; } = "gpt-4";

    /// <summary>
    /// 最大上下文 token 数
    /// </summary>
    public int MaxContextTokens { get; set; } = 8192;

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxContextTokens <= 0)
        {
            throw new InvalidOperationException(
                "MaxContextTokens must be greater than 0."
            );
        }

        if (WindowSize <= 0)
        {
            throw new InvalidOperationException(
                "WindowSize must be greater than 0."
            );
        }

        if (MaxRecentMessages <= 0)
        {
            throw new InvalidOperationException(
                "MaxRecentMessages must be greater than 0."
            );
        }

        if (SummaryThreshold <= 0)
        {
            throw new InvalidOperationException(
                "SummaryThreshold must be greater than 0."
            );
        }

        if (DowngradeThreshold <= 0)
        {
            throw new InvalidOperationException(
                "DowngradeThreshold must be greater than 0."
            );
        }

        if (RetrieveTopK <= 0)
        {
            throw new InvalidOperationException(
                "RetrieveTopK must be greater than 0."
            );
        }

        if (MinRelevanceScore < 0 || MinRelevanceScore > 1)
        {
            throw new InvalidOperationException(
                "MinRelevanceScore must be between 0 and 1."
            );
        }
    }
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

    /// <summary>
    /// 自适应记忆 - 自动从 Buffer 降级到 Summary
    /// </summary>
    Adaptive,

    /// <summary>
    /// 向量记忆 - 使用向量检索增强上下文相关性（Retrieve 策略）
    /// </summary>
    Vector,
}
