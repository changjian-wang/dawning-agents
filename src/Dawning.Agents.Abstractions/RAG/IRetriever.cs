namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// Retriever interface - encapsulates vector search and text queries.
/// </summary>
/// <remarks>
/// The retriever combines Embedding and VectorStore to provide end-to-end semantic search capabilities.
/// </remarks>
public interface IRetriever
{
    /// <summary>
    /// Retriever name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Retrieves relevant documents.
    /// </summary>
    /// <param name="query">Query text.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="minScore">Minimum similarity threshold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search results.</returns>
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves and formats results as a context string.
    /// </summary>
    /// <param name="query">Query text.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Formatted context string.</returns>
    Task<string> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    );
}
