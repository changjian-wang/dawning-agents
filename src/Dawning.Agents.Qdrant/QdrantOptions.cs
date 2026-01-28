namespace Dawning.Agents.Qdrant;

/// <summary>
/// Qdrant 配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "Qdrant": {
///     "Host": "localhost",
///     "Port": 6334,
///     "CollectionName": "documents",
///     "VectorSize": 1536,
///     "ApiKey": null
///   }
/// }
/// </code>
/// </remarks>
public class QdrantOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Qdrant";

    /// <summary>
    /// Qdrant 服务器主机名
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Qdrant gRPC 端口（默认 6334）
    /// </summary>
    public int Port { get; set; } = 6334;

    /// <summary>
    /// 集合名称
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    /// 向量维度大小
    /// </summary>
    public int VectorSize { get; set; } = 1536;

    /// <summary>
    /// API Key（可选，用于云服务认证）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// 获取完整的 gRPC 端点
    /// </summary>
    public string GetEndpoint()
    {
        var scheme = UseTls ? "https" : "http";
        return $"{scheme}://{Host}:{Port}";
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Qdrant Host is required");
        }

        if (Port <= 0 || Port > 65535)
        {
            throw new InvalidOperationException("Qdrant Port must be between 1 and 65535");
        }

        if (string.IsNullOrWhiteSpace(CollectionName))
        {
            throw new InvalidOperationException("Qdrant CollectionName is required");
        }

        if (VectorSize <= 0)
        {
            throw new InvalidOperationException("Qdrant VectorSize must be greater than 0");
        }
    }
}
