using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Chroma;

/// <summary>
/// Configuration options for the Chroma vector store.
/// </summary>
/// <remarks>
/// Configuration example (appsettings.json):
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
public class ChromaOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Chroma";

    /// <summary>
    /// The Chroma server host address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The Chroma server port.
    /// </summary>
    public int Port { get; set; } = 8000;

    /// <summary>
    /// The collection name.
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    /// The tenant name.
    /// </summary>
    public string Tenant { get; set; } = "default_tenant";

    /// <summary>
    /// The database name.
    /// </summary>
    public string Database { get; set; } = "default_database";

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTPS.
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// The API key (optional, for authentication).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The HTTP request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The vector dimension (used when creating the collection).
    /// </summary>
    public int VectorDimension { get; set; } = 1536;

    /// <summary>
    /// The distance metric.
    /// </summary>
    public ChromaDistanceMetric DistanceMetric { get; set; } = ChromaDistanceMetric.Cosine;

    /// <summary>
    /// Gets the Chroma API base URL.
    /// </summary>
    public string BaseUrl => $"{(UseHttps ? "https" : "http")}://{Host}:{Port}";

    /// <summary>
    /// Validates the configuration.
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

        if (TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Chroma TimeoutSeconds must be greater than 0");
        }
    }
}

/// <summary>
/// Chroma distance metric.
/// </summary>
public enum ChromaDistanceMetric
{
    /// <summary>
    /// Cosine similarity.
    /// </summary>
    Cosine,

    /// <summary>
    /// L2 Euclidean distance.
    /// </summary>
    L2,

    /// <summary>
    /// Inner product.
    /// </summary>
    InnerProduct,
}
