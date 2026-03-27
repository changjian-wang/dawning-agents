using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Qdrant;

/// <summary>
/// Configuration options for the Qdrant vector store.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public class QdrantOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Qdrant";

    /// <summary>
    /// The Qdrant server hostname.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// The Qdrant gRPC port (default: 6334).
    /// </summary>
    public int Port { get; set; } = 6334;

    /// <summary>
    /// The collection name.
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    /// The vector dimension size.
    /// </summary>
    public int VectorSize { get; set; } = 1536;

    /// <summary>
    /// The API key (optional, for cloud service authentication).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTPS.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Gets the full gRPC endpoint.
    /// </summary>
    public string GetEndpoint()
    {
        var scheme = UseTls ? "https" : "http";
        return $"{scheme}://{Host}:{Port}";
    }

    /// <summary>
    /// Validates the configuration.
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
