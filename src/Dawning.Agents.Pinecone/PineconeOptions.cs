namespace Dawning.Agents.Pinecone;

/// <summary>
/// Pinecone 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Pinecone": {
///     "ApiKey": "your-api-key",
///     "IndexName": "my-index",
///     "Namespace": "default",
///     "VectorSize": 1536
///   }
/// }
/// </code>
///
/// 环境变量:
/// - PINECONE_API_KEY: API 密钥
/// </remarks>
public class PineconeOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Pinecone";

    /// <summary>
    /// Pinecone API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 索引名称
    /// </summary>
    public string IndexName { get; set; } = "documents";

    /// <summary>
    /// 命名空间（用于多租户隔离）
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 向量维度大小
    /// </summary>
    public int VectorSize { get; set; } = 1536;

    /// <summary>
    /// 相似度度量方式（cosine, dotproduct, euclidean）
    /// </summary>
    public string Metric { get; set; } = "cosine";

    /// <summary>
    /// 是否在索引不存在时自动创建（Serverless 模式）
    /// </summary>
    public bool AutoCreateIndex { get; set; } = false;

    /// <summary>
    /// Serverless 云提供商（aws, gcp, azure）
    /// </summary>
    public string Cloud { get; set; } = "aws";

    /// <summary>
    /// Serverless 区域（如 us-east-1）
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Pinecone ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(IndexName))
        {
            throw new InvalidOperationException("Pinecone IndexName is required");
        }

        if (VectorSize <= 0)
        {
            throw new InvalidOperationException("Pinecone VectorSize must be positive");
        }

        var validMetrics = new[] { "cosine", "dotproduct", "euclidean" };
        if (!validMetrics.Contains(Metric.ToLowerInvariant()))
        {
            throw new InvalidOperationException(
                $"Pinecone Metric must be one of: {string.Join(", ", validMetrics)}"
            );
        }
    }
}
