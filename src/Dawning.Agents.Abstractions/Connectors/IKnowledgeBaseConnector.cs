namespace Dawning.Agents.Abstractions.Connectors;

/// <summary>
/// Knowledge base document.
/// </summary>
public record KnowledgeDocument
{
    /// <summary>
    /// Unique document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Document title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Document content (plain text or markdown).
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Document URL.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Last modified time.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>
    /// The collection / database / space this document belongs to.
    /// </summary>
    public string? Collection { get; init; }

    /// <summary>
    /// Document tags / labels.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Knowledge base search result.
/// </summary>
public record KnowledgeSearchResult
{
    /// <summary>
    /// The matched document.
    /// </summary>
    public required KnowledgeDocument Document { get; init; }

    /// <summary>
    /// Relevance score (0–1).
    /// </summary>
    public float Score { get; init; }

    /// <summary>
    /// Matching snippet / excerpt.
    /// </summary>
    public string? Snippet { get; init; }
}

/// <summary>
/// Knowledge base connector — search and retrieve documents from external knowledge bases
/// (Notion, Confluence, SharePoint, etc.).
/// </summary>
public interface IKnowledgeBaseConnector : IConnector
{
    /// <summary>
    /// Searches for documents matching a query.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="maxResults">Maximum results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a document by ID.
    /// </summary>
    /// <param name="documentId">Document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnowledgeDocument> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists available collections / databases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default);
}
