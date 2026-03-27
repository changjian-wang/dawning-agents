using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Cache;

/// <summary>
/// Semantic cache implementation using vector similarity.
/// </summary>
/// <remarks>
/// <para>Uses vector storage and embeddings to achieve semantic-level caching.</para>
/// <para>When a new query exceeds the similarity threshold against cached queries, the cached response is returned directly.</para>
/// <para>Thread-safe.</para>
/// </remarks>
public sealed class SemanticCache : ISemanticCache
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly SemanticCacheOptions _options;
    private readonly ILogger<SemanticCache> _logger;
    private readonly string _namespace;

    /// <summary>
    /// Gets the number of cache entries.
    /// </summary>
    public int Count => _vectorStore.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticCache"/> class.
    /// </summary>
    /// <param name="vectorStore">The vector store.</param>
    /// <param name="embeddingProvider">The embedding provider.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="logger">The logger (optional).</param>
    public SemanticCache(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IOptions<SemanticCacheOptions> options,
        ILogger<SemanticCache>? logger = null
    )
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embeddingProvider =
            embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? NullLogger<SemanticCache>.Instance;
        _namespace = _options.Namespace;

        _logger.LogDebug(
            "SemanticCache initialized, namespace: {Namespace}, similarity threshold: {Threshold}",
            _namespace,
            _options.SimilarityThreshold
        );
    }

    /// <summary>
    /// Attempts to retrieve a semantically similar cached response.
    /// </summary>
    public async Task<SemanticCacheResult?> GetAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        try
        {
            // Generate query embedding
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // Search for similar cache entries
            var results = await _vectorStore
                .SearchAsync(
                    queryEmbedding,
                    topK: 1,
                    minScore: _options.SimilarityThreshold,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (results.Count == 0)
            {
                _logger.LogDebug("Cache miss: {Query}", TruncateQuery(query));
                return null;
            }

            var bestMatch = results[0];
            var chunk = bestMatch.Chunk;

            // Check if expired
            if (IsExpired(chunk))
            {
                _logger.LogDebug("Cache expired: {Id}", chunk.Id);
                await _vectorStore.DeleteAsync(chunk.Id, cancellationToken).ConfigureAwait(false);
                return null;
            }

            _logger.LogDebug(
                "Cache hit: similarity={Score:F3}, original query={OriginalQuery}",
                bestMatch.Score,
                TruncateQuery(chunk.Content)
            );

            return new SemanticCacheResult
            {
                Response = chunk.Metadata.GetValueOrDefault("response", ""),
                OriginalQuery = chunk.Content,
                SimilarityScore = bestMatch.Score,
                CreatedAt = DateTimeOffset.TryParse(
                    chunk.Metadata.GetValueOrDefault("createdAt", ""),
                    out var createdAt
                )
                    ? createdAt
                    : DateTimeOffset.MinValue,
                Metadata = chunk
                    .Metadata.Where(kv => !kv.Key.StartsWith("_", StringComparison.Ordinal))
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic cache query failed");
            return null;
        }
    }

    /// <summary>
    /// Stores a query and response in the cache.
    /// </summary>
    public async Task SetAsync(
        string query,
        string response,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default
    )
    {
        if (
            !_options.Enabled
            || string.IsNullOrWhiteSpace(query)
            || string.IsNullOrWhiteSpace(response)
        )
        {
            return;
        }

        try
        {
            // Generate query embedding
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // Check if old entries need to be evicted
            if (_vectorStore.Count >= _options.MaxEntries)
            {
                // Attempt to clean up expired entries
                await EvictExpiredEntriesAsync(queryEmbedding.Length, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_vectorStore.Count >= _options.MaxEntries)
            {
                _logger.LogWarning(
                    "Cache is full (still full after eviction) ({Count}/{Max}), skipping addition",
                    _vectorStore.Count,
                    _options.MaxEntries
                );
                return;
            }

            // Build metadata
            var chunkMetadata = new Dictionary<string, string>
            {
                ["response"] = response,
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["namespace"] = _namespace,
                ["_expiresAt"] = DateTimeOffset
                    .UtcNow.AddMinutes(_options.ExpirationMinutes)
                    .ToString("O"),
            };

            if (metadata != null)
            {
                foreach (var (key, value) in metadata)
                {
                    chunkMetadata[key] = value;
                }
            }

            // Create cache entry
            var chunk = new DocumentChunk
            {
                Id = $"cache_{_namespace}_{Guid.NewGuid():N}",
                Content = query,
                Embedding = queryEmbedding,
                DocumentId = _namespace,
                Metadata = chunkMetadata,
            };

            await _vectorStore.AddAsync(chunk, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Cache entry added: {Id}, query length={QueryLen}, response length={ResponseLen}",
                chunk.Id,
                query.Length,
                response.Length
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic cache storage failed");
        }
    }

    /// <summary>
    /// Clears all cache entries.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStore
                .DeleteByDocumentIdAsync(_namespace, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Semantic cache cleared, namespace: {Namespace}", _namespace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear semantic cache");
        }
    }

    /// <summary>
    /// Checks whether a cache entry has expired.
    /// </summary>
    private bool IsExpired(DocumentChunk chunk)
    {
        if (!chunk.Metadata.TryGetValue("_expiresAt", out var expiresAtStr))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
        {
            return false;
        }

        return DateTimeOffset.UtcNow > expiresAt;
    }

    /// <summary>
    /// Evicts expired cache entries.
    /// </summary>
    private async Task EvictExpiredEntriesAsync(
        int embeddingDimension,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Search with a zero vector to get all entries; dimension must match stored embeddings
            var zeroEmbedding = new float[embeddingDimension];
            var results = await _vectorStore
                .SearchAsync(
                    zeroEmbedding,
                    topK: _options.MaxEntries,
                    minScore: 0.0f,
                    cancellationToken
                )
                .ConfigureAwait(false);

            foreach (var result in results)
            {
                if (IsExpired(result.Chunk))
                {
                    await _vectorStore
                        .DeleteAsync(result.Chunk.Id, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error evicting expired cache entries");
        }
    }

    /// <summary>
    /// Truncates a query string for logging.
    /// </summary>
    private static string TruncateQuery(string query, int maxLength = 50)
    {
        if (query.Length <= maxLength)
        {
            return query;
        }

        return query[..maxLength] + "...";
    }
}
