using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pinecone;

namespace Dawning.Agents.Pinecone;

/// <summary>
/// Pinecone 向量存储实现
/// </summary>
/// <remarks>
/// 使用 Pinecone 云向量数据库存储和检索文档嵌入。
/// Pinecone 是云原生向量数据库，支持 Serverless 和 Pod-based 部署。
///
/// 快速开始：
/// 1. 在 https://www.pinecone.io/ 注册获取 API Key
/// 2. 创建索引或启用 AutoCreateIndex
/// 3. 配置 appsettings.json 或环境变量
/// </remarks>
public sealed class PineconeVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly PineconeClient _client;
    private readonly PineconeOptions _options;
    private readonly ILogger<PineconeVectorStore> _logger;
    private object? _index; // 使用 object 因为 Index<T> 是泛型
    private bool _initialized;
    private int _count;

    public string Name => "Pinecone";

    public int Count => _count;

    /// <summary>
    /// 创建 Pinecone 向量存储
    /// </summary>
    public PineconeVectorStore(
        IOptions<PineconeOptions> options,
        ILogger<PineconeVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _options.Validate();
        _logger = logger ?? NullLogger<PineconeVectorStore>.Instance;

        _client = new PineconeClient(_options.ApiKey);

        _logger.LogDebug(
            "PineconeVectorStore 已创建，索引: {Index}，命名空间: {Namespace}",
            _options.IndexName,
            _options.Namespace ?? "(default)"
        );
    }

    /// <summary>
    /// 使用自定义客户端创建（用于测试）
    /// </summary>
    internal PineconeVectorStore(
        PineconeClient client,
        PineconeOptions options,
        ILogger<PineconeVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options;
        _logger = logger ?? NullLogger<PineconeVectorStore>.Instance;
    }

    public async Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (chunk.Embedding == null || chunk.Embedding.Length == 0)
        {
            throw new ArgumentException("DocumentChunk must have an embedding", nameof(chunk));
        }

        var index = await GetIndexAsync();

        var metadata = BuildMetadata(chunk);
        var vector = new Vector
        {
            Id = chunk.Id,
            Values = chunk.Embedding,
            Metadata = metadata,
        };

        await index.Upsert(new[] { vector }, _options.Namespace);

        Interlocked.Increment(ref _count);
        _logger.LogDebug(
            "Added chunk {ChunkId} to Pinecone index {Index}",
            chunk.Id,
            _options.IndexName
        );
    }

    public async Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var chunkList = chunks.Where(c => c.Embedding != null && c.Embedding.Length > 0).ToList();
        if (chunkList.Count == 0)
        {
            return;
        }

        var index = await GetIndexAsync();

        var vectors = chunkList
            .Select(chunk => new Vector
            {
                Id = chunk.Id,
                Values = chunk.Embedding!,
                Metadata = BuildMetadata(chunk),
            })
            .ToList();

        // Pinecone 建议每批最多 100 条
        const int batchSize = 100;
        for (var i = 0; i < vectors.Count; i += batchSize)
        {
            var batch = vectors.Skip(i).Take(batchSize).ToList();
            await index.Upsert(batch, _options.Namespace);
        }

        Interlocked.Add(ref _count, chunkList.Count);
        _logger.LogDebug(
            "Added {Count} chunks to Pinecone index {Index}",
            chunkList.Count,
            _options.IndexName
        );
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
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

        var index = await GetIndexAsync();

        var queryResponse = await index.Query(
            queryEmbedding,
            (uint)topK,
            indexNamespace: _options.Namespace,
            includeMetadata: true,
            includeValues: true
        );

        var results = new List<SearchResult>();
        if (queryResponse != null)
        {
            foreach (var match in queryResponse)
            {
                if (match.Score < minScore)
                {
                    continue;
                }

                var chunk = MatchToChunk(match);
                if (chunk != null)
                {
                    results.Add(new SearchResult { Chunk = chunk, Score = match.Score ?? 0f });
                }
            }
        }

        _logger.LogDebug(
            "Search in Pinecone returned {Count} results (topK={TopK}, minScore={MinScore})",
            results.Count,
            topK,
            minScore
        );

        return results;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var index = await GetIndexAsync();

        await index.Delete(new[] { id }, _options.Namespace);

        Interlocked.Decrement(ref _count);
        _logger.LogDebug("Deleted chunk {ChunkId} from Pinecone", id);
        return true;
    }

    public async Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var index = await GetIndexAsync();

        // Pinecone 支持按 metadata 过滤删除
        var filter = new MetadataMap { ["document_id"] = documentId };

        await index.Delete(filter, _options.Namespace);

        // Pinecone 删除不返回数量，估计为 1
        Interlocked.Decrement(ref _count);
        _logger.LogDebug("Deleted chunks for document {DocumentId} from Pinecone", documentId);
        return 1;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync();

        // 删除命名空间中的所有向量
        await index.DeleteAll(_options.Namespace);

        _count = 0;
        _logger.LogDebug(
            "Cleared Pinecone namespace {Namespace}",
            _options.Namespace ?? "(default)"
        );
    }

    public async Task<DocumentChunk?> GetAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var index = await GetIndexAsync();

        var response =
            await index.Fetch(new[] { id }, _options.Namespace) as IDictionary<string, Vector>;

        if (response == null || !response.TryGetValue(id, out Vector? vector) || vector == null)
        {
            return null;
        }

        return VectorToChunk(vector);
    }

    public async ValueTask DisposeAsync()
    {
        if (_index is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _client.Dispose();
        await Task.CompletedTask;
    }

    #region Private Helpers

    private async Task<dynamic> GetIndexAsync()
    {
        if (_initialized && _index != null)
        {
            return _index;
        }

        // 检查索引是否存在
        var indexes = await _client.ListIndexes();
        var indexExists = indexes.Any(i => i.Name == _options.IndexName);

        if (!indexExists)
        {
            if (_options.AutoCreateIndex)
            {
                // 创建 Serverless 索引
                await _client.CreateServerlessIndex(
                    _options.IndexName,
                    (uint)_options.VectorSize,
                    ParseMetric(_options.Metric),
                    _options.Cloud,
                    _options.Region
                );

                _logger.LogInformation(
                    "Created Pinecone Serverless index {Index} with {VectorSize} dimensions",
                    _options.IndexName,
                    _options.VectorSize
                );

                // 等待索引就绪
                await WaitForIndexReadyAsync();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Pinecone index '{_options.IndexName}' does not exist. "
                        + "Set AutoCreateIndex=true to create it automatically."
                );
            }
        }

        _index = await _client.GetIndex(_options.IndexName);

        // 获取当前向量数量
        var stats = await ((dynamic)_index).DescribeStats();
        _count = (int)(stats.TotalVectorCount ?? 0);

        _initialized = true;
        return _index;
    }

    private async Task WaitForIndexReadyAsync()
    {
        const int maxAttempts = 60;
        const int delayMs = 2000;

        for (var i = 0; i < maxAttempts; i++)
        {
            var indexes = await _client.ListIndexes();
            var indexInfo = indexes.FirstOrDefault(idx => idx.Name == _options.IndexName);
            if (indexInfo?.Status?.State == IndexState.Ready)
            {
                return;
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException(
            $"Pinecone index '{_options.IndexName}' did not become ready in time"
        );
    }

    private static Metric ParseMetric(string metric)
    {
        return metric.ToLowerInvariant() switch
        {
            "cosine" => Metric.Cosine,
            "dotproduct" => Metric.DotProduct,
            "euclidean" => Metric.Euclidean,
            _ => Metric.Cosine,
        };
    }

    private static MetadataMap BuildMetadata(DocumentChunk chunk)
    {
        var metadata = new MetadataMap
        {
            ["id"] = chunk.Id,
            ["content"] = chunk.Content,
            ["chunk_index"] = chunk.ChunkIndex,
        };

        if (!string.IsNullOrWhiteSpace(chunk.DocumentId))
        {
            metadata["document_id"] = chunk.DocumentId;
        }

        foreach (var (key, value) in chunk.Metadata)
        {
            metadata[$"meta_{key}"] = value;
        }

        return metadata;
    }

    private static DocumentChunk? MatchToChunk(ScoredVector match)
    {
        if (match.Metadata == null)
        {
            return null;
        }

        var id = match.Id ?? string.Empty;
        var content = GetMetadataString(match.Metadata, "content");
        var documentId = GetMetadataString(match.Metadata, "document_id");
        var chunkIndex = GetMetadataInt(match.Metadata, "chunk_index");

        var metadata = new Dictionary<string, string>();
        foreach (var (key, value) in match.Metadata)
        {
            if (key.StartsWith("meta_"))
            {
                metadata[key[5..]] = value.Inner?.ToString() ?? string.Empty;
            }
        }

        return new DocumentChunk
        {
            Id = id,
            Content = content,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = metadata,
            Embedding = match.Values?.ToArray(),
        };
    }

    private static DocumentChunk? VectorToChunk(Vector vector)
    {
        if (vector.Metadata == null)
        {
            return null;
        }

        var id = vector.Id ?? string.Empty;
        var content = GetMetadataString(vector.Metadata, "content");
        var documentId = GetMetadataString(vector.Metadata, "document_id");
        var chunkIndex = GetMetadataInt(vector.Metadata, "chunk_index");

        var metadata = new Dictionary<string, string>();
        foreach (var (key, value) in vector.Metadata)
        {
            if (key.StartsWith("meta_"))
            {
                metadata[key[5..]] = value.Inner?.ToString() ?? string.Empty;
            }
        }

        return new DocumentChunk
        {
            Id = id,
            Content = content,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = metadata,
            Embedding = vector.Values.ToArray(),
        };
    }

    private static string GetMetadataString(MetadataMap metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            ? value.Inner?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static int GetMetadataInt(MetadataMap metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value) && value.Inner != null)
        {
            return Convert.ToInt32(value.Inner);
        }
        return 0;
    }

    #endregion
}
