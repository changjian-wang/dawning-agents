using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Pinecone;

/// <summary>
/// Configuration options for the Pinecone vector store.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
/// Environment variables:
/// - PINECONE_API_KEY: API key
/// </remarks>
public class PineconeOptions : IValidatableOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Pinecone";

    /// <summary>
    /// Pinecone API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The index name.
    /// </summary>
    public string IndexName { get; set; } = "documents";

    /// <summary>
    /// The namespace for multi-tenant isolation.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// The vector dimension size.
    /// </summary>
    public int VectorSize { get; set; } = 1536;

    /// <summary>
    /// The similarity metric (cosine, dotproduct, euclidean).
    /// </summary>
    public string Metric { get; set; } = "cosine";

    /// <summary>
    /// Gets or sets a value indicating whether to auto-create the index when it does not exist (serverless mode).
    /// </summary>
    public bool AutoCreateIndex { get; set; } = false;

    /// <summary>
    /// The serverless cloud provider (aws, gcp, azure).
    /// </summary>
    public string Cloud { get; set; } = "aws";

    /// <summary>
    /// The serverless region (e.g., us-east-1).
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Validates the configuration.
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
