namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// Embedding vector provider interface.
/// </summary>
/// <remarks>
/// Converts text into vector representations for semantic search and similarity computation.
/// </remarks>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Vector dimensions.
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// Generates an embedding vector for a single text.
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Embedding vector.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple texts in batch.
    /// </summary>
    /// <param name="texts">Input text list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of embedding vectors.</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    );
}
