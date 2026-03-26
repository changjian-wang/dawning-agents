using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// RAG configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public class RAGOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "RAG";

    /// <summary>
    /// Document chunk size (in characters).
    /// </summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>
    /// Overlap size between chunks (in characters).
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// Default number of results to return.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum similarity threshold (0-1).
    /// </summary>
    public float MinScore { get; set; } = 0.5f;

    /// <summary>
    /// Embedding model name.
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Whether to include metadata in retrieval results.
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Context formatting template.
    /// </summary>
    /// <remarks>
    /// Available placeholders: {index}, {content}, {score}, {source}
    /// </remarks>
    public string ContextTemplate { get; set; } = "[{index}] {content}";

    /// <summary>
    /// Validates the configuration.
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

        if (string.IsNullOrWhiteSpace(EmbeddingModel))
        {
            throw new InvalidOperationException("EmbeddingModel is required");
        }

        if (string.IsNullOrWhiteSpace(ContextTemplate))
        {
            throw new InvalidOperationException("ContextTemplate is required");
        }
    }
}
