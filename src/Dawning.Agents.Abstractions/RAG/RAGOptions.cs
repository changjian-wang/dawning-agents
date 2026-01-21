namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// RAG 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "RAG": {
///     "ChunkSize": 500,
///     "ChunkOverlap": 50,
///     "TopK": 5,
///     "MinScore": 0.5,
///     "EmbeddingModel": "text-embedding-3-small"
///   }
/// }
/// </code>
/// </remarks>
public class RAGOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "RAG";

    /// <summary>
    /// 文档分块大小（字符数）
    /// </summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>
    /// 块之间的重叠大小（字符数）
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// 默认返回的结果数量
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// 最小相似度阈值 (0-1)
    /// </summary>
    public float MinScore { get; set; } = 0.5f;

    /// <summary>
    /// 嵌入模型名称
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// 是否在检索结果中包含元数据
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// 上下文格式化模板
    /// </summary>
    /// <remarks>
    /// 可用占位符: {index}, {content}, {score}, {source}
    /// </remarks>
    public string ContextTemplate { get; set; } = "[{index}] {content}";

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (ChunkSize <= 0)
        {
            throw new InvalidOperationException("ChunkSize must be greater than 0");
        }

        if (ChunkOverlap < 0)
        {
            throw new InvalidOperationException("ChunkOverlap cannot be negative");
        }

        if (ChunkOverlap >= ChunkSize)
        {
            throw new InvalidOperationException("ChunkOverlap must be less than ChunkSize");
        }

        if (TopK <= 0)
        {
            throw new InvalidOperationException("TopK must be greater than 0");
        }

        if (MinScore < 0 || MinScore > 1)
        {
            throw new InvalidOperationException("MinScore must be between 0 and 1");
        }
    }
}
