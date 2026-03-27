using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Knowledge base that integrates document management, chunking, embedding, and retrieval.
/// </summary>
/// <remarks>
/// Provides an end-to-end RAG workflow:
/// <list type="number">
///   <item>Add document -> auto-chunk -> generate embeddings -> store.</item>
///   <item>Query -> semantic retrieval -> return relevant content.</item>
/// </list>
/// </remarks>
public sealed class KnowledgeBase
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunker _chunker;
    private readonly RAGOptions _options;
    private readonly ILogger<KnowledgeBase> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBase"/> class.
    /// </summary>
    public KnowledgeBase(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        DocumentChunker? chunker = null,
        IOptions<RAGOptions>? options = null,
        ILogger<KnowledgeBase>? logger = null
    )
    {
        _embeddingProvider =
            embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _chunker = chunker ?? new DocumentChunker(options);
        _options = options?.Value ?? new RAGOptions();
        _logger = logger ?? NullLogger<KnowledgeBase>.Instance;
    }

    /// <summary>
    /// Gets the knowledge base name.
    /// </summary>
    public string Name => $"KnowledgeBase({_vectorStore.Name})";

    /// <summary>
    /// Gets the number of document chunks.
    /// </summary>
    public int ChunkCount => _vectorStore.Count;

    /// <summary>
    /// Adds a document to the knowledge base.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <param name="documentId">Optional document ID. Auto-generated if not specified.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of chunks added.</returns>
    public async Task<int> AddDocumentAsync(
        string content,
        string? documentId = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        documentId ??= Guid.NewGuid().ToString("N");
        metadata ??= [];

        _logger.LogDebug("Adding document {DocumentId} to knowledge base", documentId);

        // 1. Chunk
        var chunks = _chunker.ChunkText(content, documentId, metadata);

        if (chunks.Count == 0)
        {
            return 0;
        }

        // 2. Generate embeddings in batch
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider
            .EmbedBatchAsync(texts, cancellationToken)
            .ConfigureAwait(false);

        var embeddingList = embeddings.ToList();
        if (embeddingList.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Embedding count mismatch: expected {chunks.Count}, got {embeddingList.Count}"
            );
        }

        // 3. Attach embedding vectors to chunks
        var chunksWithEmbeddings = chunks
            .Zip(embeddingList, (chunk, embedding) => chunk with { Embedding = embedding })
            .ToList();

        // 4. Store in vector database
        await _vectorStore
            .AddBatchAsync(chunksWithEmbeddings, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Added document {DocumentId} with {ChunkCount} chunks to knowledge base",
            documentId,
            chunks.Count
        );

        return chunks.Count;
    }

    /// <summary>
    /// Adds a document from a file.
    /// </summary>
    public async Task<int> AddDocumentFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken)
            .ConfigureAwait(false);
        var documentId = Path.GetFileName(filePath);
        var metadata = new Dictionary<string, string>
        {
            ["source"] = filePath,
            ["filename"] = Path.GetFileName(filePath),
            ["extension"] = Path.GetExtension(filePath),
        };

        return await AddDocumentAsync(content, documentId, metadata, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Queries the knowledge base.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search results.</returns>
    public async Task<IReadOnlyList<SearchResult>> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        _logger.LogDebug("Querying knowledge base: {Query}", query);

        // Generate query embedding
        var queryEmbedding = await _embeddingProvider
            .EmbedAsync(query, cancellationToken)
            .ConfigureAwait(false);

        // Vector search
        var results = await _vectorStore
            .SearchAsync(queryEmbedding, topK, _options.MinScore, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Query returned {Count} results", results.Count);

        return results;
    }

    /// <summary>
    /// Queries and returns formatted context.
    /// </summary>
    public async Task<string> QueryContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        var results = await QueryAsync(query, topK, cancellationToken).ConfigureAwait(false);

        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n\n",
            results.Select((r, i) => $"[{i + 1}] (score: {r.Score:F2})\n{r.Chunk.Content}")
        );
    }

    /// <summary>
    /// Removes a document.
    /// </summary>
    public Task<int> DeleteDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        return _vectorStore.DeleteByDocumentIdAsync(documentId, cancellationToken);
    }

    /// <summary>
    /// Clears the knowledge base.
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        return _vectorStore.ClearAsync(cancellationToken);
    }
}
