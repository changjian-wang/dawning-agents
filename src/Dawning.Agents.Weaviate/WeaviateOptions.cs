namespace Dawning.Agents.Weaviate;

/// <summary>
/// Weaviate 向量存储配置选项
/// </summary>
/// <remarks>
/// 配置示例 (appsettings.json):
/// <code>
/// {
///   "Weaviate": {
///     "Host": "localhost",
///     "Port": 8080,
///     "ClassName": "Document",
///     "Scheme": "http",
///     "ApiKey": null
///   }
/// }
/// </code>
/// </remarks>
public class WeaviateOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Weaviate";

    /// <summary>
    /// Weaviate 服务器主机地址
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Weaviate 服务器端口（REST API）
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// gRPC 端口
    /// </summary>
    public int GrpcPort { get; set; } = 50051;

    /// <summary>
    /// Schema 类名（Weaviate 中的集合概念）
    /// </summary>
    public string ClassName { get; set; } = "Document";

    /// <summary>
    /// 连接协议 (http 或 https)
    /// </summary>
    public string Scheme { get; set; } = "http";

    /// <summary>
    /// API 密钥（可选，用于认证）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// HTTP 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 向量维度
    /// </summary>
    public int VectorDimension { get; set; } = 1536;

    /// <summary>
    /// 距离度量方式
    /// </summary>
    public WeaviateDistanceMetric DistanceMetric { get; set; } = WeaviateDistanceMetric.Cosine;

    /// <summary>
    /// 向量索引类型
    /// </summary>
    public WeaviateVectorIndexType VectorIndexType { get; set; } = WeaviateVectorIndexType.Hnsw;

    /// <summary>
    /// 获取 Weaviate REST API 基础 URL
    /// </summary>
    public string BaseUrl => $"{Scheme}://{Host}:{Port}";

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Weaviate Host is required");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Weaviate Port must be between 1 and 65535");
        }

        if (string.IsNullOrWhiteSpace(ClassName))
        {
            throw new InvalidOperationException("Weaviate ClassName is required");
        }

        if (VectorDimension <= 0)
        {
            throw new InvalidOperationException("VectorDimension must be greater than 0");
        }
    }
}

/// <summary>
/// Weaviate 距离度量方式
/// </summary>
public enum WeaviateDistanceMetric
{
    /// <summary>
    /// 余弦距离（默认）
    /// </summary>
    Cosine,

    /// <summary>
    /// 点积
    /// </summary>
    Dot,

    /// <summary>
    /// L2 欧几里得距离
    /// </summary>
    L2Squared,

    /// <summary>
    /// 汉明距离
    /// </summary>
    Hamming,

    /// <summary>
    /// 曼哈顿距离
    /// </summary>
    Manhattan,
}

/// <summary>
/// Weaviate 向量索引类型
/// </summary>
public enum WeaviateVectorIndexType
{
    /// <summary>
    /// HNSW 索引（默认，高性能近似最近邻搜索）
    /// </summary>
    Hnsw,

    /// <summary>
    /// Flat 索引（精确搜索，适合小数据集）
    /// </summary>
    Flat,

    /// <summary>
    /// Dynamic 索引（自动选择）
    /// </summary>
    Dynamic,
}
