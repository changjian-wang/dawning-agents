namespace Dawning.Agents.Chroma;

/// <summary>
/// Chroma 向量存储配置选项
/// </summary>
/// <remarks>
/// 配置示例 (appsettings.json):
/// <code>
/// {
///   "Chroma": {
///     "Host": "localhost",
///     "Port": 8000,
///     "CollectionName": "documents",
///     "Tenant": "default_tenant",
///     "Database": "default_database"
///   }
/// }
/// </code>
/// </remarks>
public class ChromaOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Chroma";

    /// <summary>
    /// Chroma 服务器主机地址
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Chroma 服务器端口
    /// </summary>
    public int Port { get; set; } = 8000;

    /// <summary>
    /// 集合名称
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    /// 租户名称
    /// </summary>
    public string Tenant { get; set; } = "default_tenant";

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string Database { get; set; } = "default_database";

    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// API 密钥（可选，用于认证）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// HTTP 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 向量维度（用于创建集合）
    /// </summary>
    public int VectorDimension { get; set; } = 1536;

    /// <summary>
    /// 距离度量方式
    /// </summary>
    public ChromaDistanceMetric DistanceMetric { get; set; } = ChromaDistanceMetric.Cosine;

    /// <summary>
    /// 获取 Chroma API 基础 URL
    /// </summary>
    public string BaseUrl =>
        $"{(UseHttps ? "https" : "http")}://{Host}:{Port}";

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Chroma Host is required");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Chroma Port must be between 1 and 65535");
        }

        if (string.IsNullOrWhiteSpace(CollectionName))
        {
            throw new InvalidOperationException("Chroma CollectionName is required");
        }

        if (VectorDimension <= 0)
        {
            throw new InvalidOperationException("VectorDimension must be positive");
        }
    }
}

/// <summary>
/// Chroma 距离度量方式
/// </summary>
public enum ChromaDistanceMetric
{
    /// <summary>
    /// 余弦相似度
    /// </summary>
    Cosine,

    /// <summary>
    /// L2 欧几里得距离
    /// </summary>
    L2,

    /// <summary>
    /// 内积
    /// </summary>
    InnerProduct
}
