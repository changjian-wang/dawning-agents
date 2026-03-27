using System.Collections.Concurrent;
using System.Numerics.Tensors;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// In-memory vector store implementation.
/// </summary>
/// <remarks>
/// Suitable for small datasets or development and testing scenarios.
/// Data is stored in memory and is lost on restart.
/// Uses SIMD-accelerated cosine similarity computation.
/// </remarks>
public sealed class InMemoryVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, DocumentChunk> _chunks = new();
    private readonly ILogger<InMemoryVectorStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryVectorStore"/> class.
    /// </summary>
    public InMemoryVectorStore(ILogger<InMemoryVectorStore>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryVectorStore>.Instance;
    }

    /// <inheritdoc />
    public string Name => "InMemory";

    /// <inheritdoc />
    public int Count => _chunks.Count;

    /// <inheritdoc />
    public Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (chunk.Embedding == null || chunk.Embedding.Length == 0)
        {
            throw new ArgumentException("DocumentChunk must have an embedding", nameof(chunk));
        }

        _chunks[chunk.Id] = chunk;
        _logger.LogDebug("Added chunk {ChunkId} to vector store", chunk.Id);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var count = 0;
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunk.Embedding == null || chunk.Embedding.Length == 0)
            {
                _logger.LogWarning("Skipping chunk {ChunkId} - no embedding", chunk.Id);
                continue;
            }

            _chunks[chunk.Id] = chunk;
            count++;
        }

        _logger.LogDebug("Added {Count} chunks to vector store", count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (queryEmbedding.Length == 0)
        {
            throw new ArgumentException("Query embedding cannot be empty", nameof(queryEmbedding));
        }

        var results = _chunks
            .Values.Where(c => c.Embedding != null)
            .Select(chunk =>
            {
                var score = CosineSimilarity(queryEmbedding, chunk.Embedding!);
                return new SearchResult { Chunk = chunk, Score = score };
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        _logger.LogDebug(
            "Search returned {Count} results (topK={TopK}, minScore={MinScore})",
            results.Count,
            topK,
            minScore
        );

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var removed = _chunks.TryRemove(id, out _);
        if (removed)
        {
            _logger.LogDebug("Deleted chunk {ChunkId}", id);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var toRemove = _chunks
            .Values.Where(c => c.DocumentId == documentId)
            .Select(c => c.Id)
            .ToList();

        var count = 0;
        foreach (var id in toRemove)
        {
            if (_chunks.TryRemove(id, out _))
            {
                count++;
            }
        }

        _logger.LogDebug("Deleted {Count} chunks for document {DocumentId}", count, documentId);
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var count = _chunks.Count;
        _chunks.Clear();
        _logger.LogDebug("Cleared vector store ({Count} chunks removed)", count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DocumentChunk?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _chunks.TryGetValue(id, out var chunk);
        return Task.FromResult(chunk);
    }

    /// <summary>
    /// Computes cosine similarity using SIMD acceleration.
    /// </summary>
    /// <param name="a">Vector A.</param>
    /// <param name="b">Vector B.</param>
    /// <returns>Similarity score (0-1), normalized.</returns>
    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        // Use System.Numerics.Tensors SIMD-optimized implementation
        var similarity = TensorPrimitives.CosineSimilarity(a, b);

        // Handle NaN (occurs with zero vectors)
        if (float.IsNaN(similarity))
        {
            return 0;
        }

        // Cosine similarity range is [-1, 1], normalize to [0, 1]
        return (similarity + 1) / 2;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _chunks.Clear();
        return ValueTask.CompletedTask;
    }
}
