using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Dawning.Agents.Qdrant;

/// <summary>
/// Qdrant 向量存储实现
/// </summary>
/// <remarks>
/// 使用 Qdrant 向量数据库存储和检索文档嵌入。
/// 支持本地部署和 Qdrant Cloud。
///
/// 安装 Qdrant（Docker）:
/// <code>
/// docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
/// </code>
/// </remarks>
public sealed class QdrantVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorStore> _logger;
    private bool _collectionInitialized;
    private int _count;

    public string Name => "Qdrant";

    public int Count => _count;

    /// <summary>
    /// 创建 Qdrant 向量存储
    /// </summary>
    public QdrantVectorStore(
        IOptions<QdrantOptions> options,
        ILogger<QdrantVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _options.Validate();
        _logger = logger ?? NullLogger<QdrantVectorStore>.Instance;

        _client = CreateClient();

        _logger.LogDebug(
            "QdrantVectorStore 已创建，端点: {Host}:{Port}，集合: {Collection}",
            _options.Host,
            _options.Port,
            _options.CollectionName
        );
    }

    /// <summary>
    /// 使用自定义客户端创建（用于测试）
    /// </summary>
    internal QdrantVectorStore(
        QdrantClient client,
        QdrantOptions options,
        ILogger<QdrantVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options;
        _logger = logger ?? NullLogger<QdrantVectorStore>.Instance;
    }

    public async Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (chunk.Embedding == null || chunk.Embedding.Length == 0)
        {
            throw new ArgumentException("DocumentChunk must have an embedding", nameof(chunk));
        }

        await EnsureCollectionExistsAsync(cancellationToken);

        var point = CreatePoint(chunk);
        await _client.UpsertAsync(
            _options.CollectionName,
            new[] { point },
            cancellationToken: cancellationToken
        );

        Interlocked.Increment(ref _count);
        _logger.LogDebug("Added chunk {ChunkId} to Qdrant collection {Collection}", chunk.Id, _options.CollectionName);
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

        await EnsureCollectionExistsAsync(cancellationToken);

        var points = chunkList.Select(CreatePoint).ToList();
        await _client.UpsertAsync(
            _options.CollectionName,
            points,
            cancellationToken: cancellationToken
        );

        Interlocked.Add(ref _count, chunkList.Count);
        _logger.LogDebug("Added {Count} chunks to Qdrant collection {Collection}", chunkList.Count, _options.CollectionName);
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

        await EnsureCollectionExistsAsync(cancellationToken);

        var searchResults = await _client.SearchAsync(
            _options.CollectionName,
            queryEmbedding,
            limit: (ulong)topK,
            scoreThreshold: minScore,
            cancellationToken: cancellationToken
        );

        var results = new List<SearchResult>();
        foreach (var result in searchResults)
        {
            var chunk = PointToChunk(result);
            if (chunk != null)
            {
                results.Add(new SearchResult
                {
                    Chunk = chunk,
                    Score = result.Score
                });
            }
        }

        _logger.LogDebug(
            "Search in Qdrant returned {Count} results (topK={TopK}, minScore={MinScore})",
            results.Count,
            topK,
            minScore
        );

        return results;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureCollectionExistsAsync(cancellationToken);

        // 使用过滤器删除
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "id",
                        Match = new Match { Keyword = id }
                    }
                }
            }
        };

        var updateResult = await _client.DeleteAsync(
            _options.CollectionName,
            filter,
            cancellationToken: cancellationToken
        );

        if (updateResult.Status == UpdateStatus.Completed)
        {
            Interlocked.Decrement(ref _count);
            _logger.LogDebug("Deleted chunk {ChunkId} from Qdrant", id);
            return true;
        }

        return false;
    }

    public async Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        await EnsureCollectionExistsAsync(cancellationToken);

        // 使用过滤条件删除
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "document_id",
                        Match = new Match { Keyword = documentId }
                    }
                }
            }
        };

        // 先搜索获取数量
        var scrollResult = await _client.ScrollAsync(
            _options.CollectionName,
            filter: filter,
            limit: 10000,
            cancellationToken: cancellationToken
        );

        var count = scrollResult.Result.Count;

        if (count > 0)
        {
            await _client.DeleteAsync(
                _options.CollectionName,
                filter,
                cancellationToken: cancellationToken
            );

            Interlocked.Add(ref _count, -count);
            _logger.LogDebug("Deleted {Count} chunks for document {DocumentId}", count, documentId);
        }

        return count;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        // 删除并重新创建集合
        try
        {
            await _client.DeleteCollectionAsync(_options.CollectionName, null, cancellationToken);
        }
        catch
        {
            // 集合可能不存在，忽略错误
        }

        _collectionInitialized = false;
        _count = 0;

        await EnsureCollectionExistsAsync(cancellationToken);

        _logger.LogDebug("Cleared Qdrant collection {Collection}", _options.CollectionName);
    }

    public async Task<DocumentChunk?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await EnsureCollectionExistsAsync(cancellationToken);

        // 使用过滤器搜索
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "id",
                        Match = new Match { Keyword = id }
                    }
                }
            }
        };

        var scrollResult = await _client.ScrollAsync(
            _options.CollectionName,
            filter: filter,
            limit: 1,
            payloadSelector: true,
            vectorsSelector: true,
            cancellationToken: cancellationToken
        );

        var point = scrollResult.Result.FirstOrDefault();
        return point != null ? RetrievedPointToChunk(point) : null;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    #region Private Helpers

    private QdrantClient CreateClient()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new QdrantClient(
                _options.Host,
                _options.Port,
                https: _options.UseTls,
                apiKey: _options.ApiKey
            );
        }

        return new QdrantClient(_options.Host, _options.Port, https: _options.UseTls);
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        if (_collectionInitialized)
        {
            return;
        }

        var exists = await _client.CollectionExistsAsync(_options.CollectionName, cancellationToken);
        if (!exists)
        {
            await _client.CreateCollectionAsync(
                _options.CollectionName,
                new VectorParams
                {
                    Size = (ulong)_options.VectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken
            );

            _logger.LogInformation(
                "Created Qdrant collection {Collection} with vector size {VectorSize}",
                _options.CollectionName,
                _options.VectorSize
            );
        }
        else
        {
            // 获取当前数量
            var info = await _client.GetCollectionInfoAsync(_options.CollectionName, cancellationToken);
            _count = (int)info.PointsCount;
        }

        _collectionInitialized = true;
    }

    private PointStruct CreatePoint(DocumentChunk chunk)
    {
        var payload = new Dictionary<string, Value>
        {
            ["id"] = chunk.Id,
            ["content"] = chunk.Content,
            ["chunk_index"] = chunk.ChunkIndex
        };

        if (!string.IsNullOrWhiteSpace(chunk.DocumentId))
        {
            payload["document_id"] = chunk.DocumentId;
        }

        foreach (var (key, value) in chunk.Metadata)
        {
            payload[$"meta_{key}"] = value;
        }

        // 使用稳定的 GUID 作为点 ID
        var pointId = GetPointId(chunk.Id);

        return new PointStruct
        {
            Id = pointId,
            Vectors = chunk.Embedding!,
            Payload = { payload }
        };
    }

    private static Guid GetPointId(string id)
    {
        // 尝试解析为 GUID，否则使用哈希
        if (Guid.TryParse(id, out var guid))
        {
            return guid;
        }

        // 使用稳定的哈希算法生成 UUID
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id));
        return new Guid(hash);
    }

    private static DocumentChunk? PointToChunk(ScoredPoint point)
    {
        if (!point.Payload.TryGetValue("id", out var idValue))
        {
            return null;
        }

        var id = idValue.StringValue;
        var content = point.Payload.TryGetValue("content", out var contentValue)
            ? contentValue.StringValue
            : string.Empty;

        var documentId = point.Payload.TryGetValue("document_id", out var docIdValue)
            ? docIdValue.StringValue
            : null;

        var chunkIndex = point.Payload.TryGetValue("chunk_index", out var indexValue)
            ? (int)indexValue.IntegerValue
            : 0;

        var metadata = new Dictionary<string, string>();
        foreach (var (key, value) in point.Payload)
        {
            if (key.StartsWith("meta_"))
            {
                metadata[key[5..]] = value.StringValue;
            }
        }

        return new DocumentChunk
        {
            Id = id,
            Content = content,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = metadata,
            Embedding = point.Vectors?.Vector?.Data?.ToArray()
        };
    }

    private static DocumentChunk? RetrievedPointToChunk(RetrievedPoint point)
    {
        if (!point.Payload.TryGetValue("id", out var idValue))
        {
            return null;
        }

        var id = idValue.StringValue;
        var content = point.Payload.TryGetValue("content", out var contentValue)
            ? contentValue.StringValue
            : string.Empty;

        var documentId = point.Payload.TryGetValue("document_id", out var docIdValue)
            ? docIdValue.StringValue
            : null;

        var chunkIndex = point.Payload.TryGetValue("chunk_index", out var indexValue)
            ? (int)indexValue.IntegerValue
            : 0;

        var metadata = new Dictionary<string, string>();
        foreach (var (key, value) in point.Payload)
        {
            if (key.StartsWith("meta_"))
            {
                metadata[key[5..]] = value.StringValue;
            }
        }

        return new DocumentChunk
        {
            Id = id,
            Content = content,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = metadata,
            Embedding = point.Vectors?.Vector?.Data?.ToArray()
        };
    }

    #endregion
}
