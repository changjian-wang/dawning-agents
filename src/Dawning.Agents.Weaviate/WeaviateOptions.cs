using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Weaviate;

/// <summary>
/// Configuration options for the Weaviate vector store.
/// </summary>
/// <remarks>
/// Configuration example (appsettings.json):
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
public class WeaviateOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Weaviate";

    /// <summary>
    /// The Weaviate server host address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The Weaviate server port (REST API).
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// The gRPC port.
    /// </summary>
    public int GrpcPort { get; set; } = 50051;

    /// <summary>
    /// The schema class name (equivalent to a collection in Weaviate).
    /// </summary>
    public string ClassName { get; set; } = "Document";

    /// <summary>
    /// The connection scheme (http or https).
    /// </summary>
    public string Scheme { get; set; } = "http";

    /// <summary>
    /// The API key (optional, for authentication).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The HTTP request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The vector dimension.
    /// </summary>
    public int VectorDimension { get; set; } = 1536;

    /// <summary>
    /// The distance metric.
    /// </summary>
    public WeaviateDistanceMetric DistanceMetric { get; set; } = WeaviateDistanceMetric.Cosine;

    /// <summary>
    /// The vector index type.
    /// </summary>
    public WeaviateVectorIndexType VectorIndexType { get; set; } = WeaviateVectorIndexType.Hnsw;

    /// <summary>
    /// Gets the Weaviate REST API base URL.
    /// </summary>
    public string BaseUrl => $"{Scheme}://{Host}:{Port}";

    /// <summary>
    /// Validates the configuration.
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

        if (Scheme is not "http" and not "https")
        {
            throw new InvalidOperationException("Weaviate Scheme must be 'http' or 'https'");
        }

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Weaviate TimeoutSeconds must be greater than 0");
        }

        if (VectorDimension <= 0)
        {
            throw new InvalidOperationException("VectorDimension must be greater than 0");
        }
    }
}

/// <summary>
/// Weaviate distance metric.
/// </summary>
public enum WeaviateDistanceMetric
{
    /// <summary>
    /// Cosine distance (default).
    /// </summary>
    Cosine,

    /// <summary>
    /// Dot product.
    /// </summary>
    Dot,

    /// <summary>
    /// L2 squared Euclidean distance.
    /// </summary>
    L2Squared,

    /// <summary>
    /// Hamming distance.
    /// </summary>
    Hamming,

    /// <summary>
    /// Manhattan distance.
    /// </summary>
    Manhattan,
}

/// <summary>
/// Weaviate vector index type.
/// </summary>
public enum WeaviateVectorIndexType
{
    /// <summary>
    /// HNSW index (default, high-performance approximate nearest neighbor search).
    /// </summary>
    Hnsw,

    /// <summary>
    /// Flat index (exact search, suitable for small datasets).
    /// </summary>
    Flat,

    /// <summary>
    /// Dynamic index (automatically selected).
    /// </summary>
    Dynamic,
}
