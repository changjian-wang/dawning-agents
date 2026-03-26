namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// Document chunk - the smallest unit stored in a vector database.
/// </summary>
public record DocumentChunk
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Text content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Embedding vector.
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    /// Metadata (source file, page number, etc.).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Parent document ID.
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// Chunk index within the document.
    /// </summary>
    public int ChunkIndex { get; init; }
}

/// <summary>
/// Search result.
/// </summary>
public record SearchResult
{
    /// <summary>
    /// Document chunk.
    /// </summary>
    public required DocumentChunk Chunk { get; init; }

    /// <summary>
    /// Similarity score (0-1, higher means more similar).
    /// </summary>
    public required float Score { get; init; }
}

/// <summary>
/// Vector store interface.
/// </summary>
/// <remarks>
/// Provides storage, retrieval, and deletion of vectors.
/// </remarks>
public interface IVectorStore
{
    /// <summary>
    /// Store name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Number of stored document chunks.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Adds a document chunk.
    /// </summary>
    /// <param name="chunk">Document chunk (must include embedding vector).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds document chunks in batch.
    /// </summary>
    /// <param name="chunks">List of document chunks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Searches for similar documents by vector.
    /// </summary>
    /// <param name="queryEmbedding">Query vector.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="minScore">Minimum similarity threshold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results (sorted by similarity in descending order).</returns>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes a document chunk.
    /// </summary>
    /// <param name="id">Document chunk ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the deletion was successful.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chunks belonging to a document.
    /// </summary>
    /// <param name="documentId">Document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks deleted.</returns>
    Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Clears all data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document chunk.
    /// </summary>
    /// <param name="id">Document chunk ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document chunk, or null if not found.</returns>
    Task<DocumentChunk?> GetAsync(string id, CancellationToken cancellationToken = default);
}
