using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Chroma;

/// <summary>
/// Chroma 向量存储实现
/// </summary>
/// <remarks>
/// Chroma 是一个轻量级的开源向量数据库，适合开发和测试场景。
///
/// 安装 Chroma（Docker）:
/// <code>
/// docker run -p 8000:8000 chromadb/chroma
/// </code>
///
/// 或使用 Python:
/// <code>
/// pip install chromadb
/// chroma run --host localhost --port 8000
/// </code>
/// </remarks>
public sealed class ChromaVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ChromaOptions _options;
    private readonly ILogger<ChromaVectorStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _collectionId;
    private int _count;

    public string Name => "Chroma";
    public int Count => _count;

    /// <summary>
    /// 创建 Chroma 向量存储
    /// </summary>
    public ChromaVectorStore(
        HttpClient httpClient,
        IOptions<ChromaOptions> options,
        ILogger<ChromaVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();
        _logger = logger ?? NullLogger<ChromaVectorStore>.Instance;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
        }

        _logger.LogDebug(
            "ChromaVectorStore initialized: {BaseUrl}, Collection={Collection}",
            _options.BaseUrl,
            _options.CollectionName
        );
    }

    /// <inheritdoc />
    public async Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);
        await AddBatchAsync([chunk], cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCollectionAsync(cancellationToken);

        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
        {
            return;
        }

        var ids = new List<string>();
        var embeddings = new List<float[]>();
        var documents = new List<string>();
        var metadatas = new List<Dictionary<string, object>>();

        foreach (var chunk in chunkList)
        {
            if (chunk.Embedding == null || chunk.Embedding.Length == 0)
            {
                throw new ArgumentException($"Chunk {chunk.Id} has no embedding");
            }

            ids.Add(chunk.Id);
            embeddings.Add(chunk.Embedding);
            documents.Add(chunk.Content);

            var metadata = new Dictionary<string, object>();
            foreach (var kv in chunk.Metadata)
            {
                metadata[kv.Key] = kv.Value;
            }

            if (chunk.DocumentId != null)
            {
                metadata["document_id"] = chunk.DocumentId;
            }

            metadata["chunk_index"] = chunk.ChunkIndex;
            metadatas.Add(metadata);
        }

        var request = new
        {
            ids,
            embeddings,
            documents,
            metadatas,
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionId}/add",
            request,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Chroma add failed: {response.StatusCode} - {error}");
        }

        _count += chunkList.Count;

        _logger.LogDebug("Added {Count} chunks to Chroma collection {Collection}", chunkList.Count, _options.CollectionName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            query_embeddings = new[] { queryEmbedding },
            n_results = topK,
            include = new[] { "embeddings", "documents", "metadatas", "distances" },
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionId}/query",
            request,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Chroma query failed: {response.StatusCode} - {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChromaQueryResponse>(
            _jsonOptions,
            cancellationToken
        );

        if (result == null || result.Ids == null || result.Ids.Count == 0 || result.Ids[0].Count == 0)
        {
            return [];
        }

        var searchResults = new List<SearchResult>();
        var ids = result.Ids[0];
        var distances = result.Distances?[0] ?? [];
        var documents = result.Documents?[0] ?? [];
        var metadatas = result.Metadatas?[0] ?? [];
        var embeddings = result.Embeddings?[0] ?? [];

        for (int i = 0; i < ids.Count; i++)
        {
            // Chroma 返回距离，需要转换为相似度分数
            var distance = i < distances.Count ? distances[i] : 0f;
            var score = ConvertDistanceToScore(distance);

            if (score < minScore)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>();
            string? documentId = null;
            int chunkIndex = 0;

            if (i < metadatas.Count && metadatas[i] != null)
            {
                foreach (var kv in metadatas[i])
                {
                    if (kv.Key == "document_id")
                    {
                        documentId = kv.Value?.ToString();
                    }
                    else if (kv.Key == "chunk_index")
                    {
                        int.TryParse(kv.Value?.ToString(), out chunkIndex);
                    }
                    else if (kv.Value != null)
                    {
                        metadata[kv.Key] = kv.Value.ToString()!;
                    }
                }
            }

            var chunk = new DocumentChunk
            {
                Id = ids[i],
                Content = i < documents.Count ? documents[i] : "",
                Embedding = i < embeddings.Count ? embeddings[i] : null,
                Metadata = metadata,
                DocumentId = documentId,
                ChunkIndex = chunkIndex,
            };

            searchResults.Add(new SearchResult { Chunk = chunk, Score = score });
        }

        _logger.LogDebug(
            "Searched Chroma collection {Collection}, found {Count} results",
            _options.CollectionName,
            searchResults.Count
        );

        return searchResults.OrderByDescending(r => r.Score).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new { ids = new[] { id } };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionId}/delete",
            request,
            _jsonOptions,
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
        {
            _count = Math.Max(0, _count - 1);
            _logger.LogDebug("Deleted chunk {Id} from Chroma collection {Collection}", id, _options.CollectionName);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<DocumentChunk?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            ids = new[] { id },
            include = new[] { "embeddings", "documents", "metadatas" },
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionId}/get",
            request,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<ChromaGetResponse>(
            _jsonOptions,
            cancellationToken
        );

        if (result?.Ids == null || result.Ids.Count == 0)
        {
            return null;
        }

        var metadata = new Dictionary<string, string>();
        string? documentId = null;
        int chunkIndex = 0;

        if (result.Metadatas != null && result.Metadatas.Count > 0 && result.Metadatas[0] != null)
        {
            foreach (var kv in result.Metadatas[0])
            {
                if (kv.Key == "document_id")
                {
                    documentId = kv.Value?.ToString();
                }
                else if (kv.Key == "chunk_index")
                {
                    int.TryParse(kv.Value?.ToString(), out chunkIndex);
                }
                else if (kv.Value != null)
                {
                    metadata[kv.Key] = kv.Value.ToString()!;
                }
            }
        }

        return new DocumentChunk
        {
            Id = result.Ids[0],
            Content = result.Documents != null && result.Documents.Count > 0 ? result.Documents[0] : "",
            Embedding = result.Embeddings != null && result.Embeddings.Count > 0 ? result.Embeddings[0] : null,
            Metadata = metadata,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
        };
    }

    /// <inheritdoc />
    public async Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCollectionAsync(cancellationToken);

        var request = new
        {
            where = new Dictionary<string, object> { ["document_id"] = documentId },
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/collections/{_collectionId}/delete",
            request,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        // Chroma 不返回删除数量，我们无法精确知道删除了多少
        _logger.LogDebug(
            "Deleted chunks for document {DocumentId} from Chroma collection {Collection}",
            documentId,
            _options.CollectionName
        );

        return -1; // 表示删除成功但数量未知
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionId == null)
        {
            return;
        }

        // 删除并重新创建集合
        var deleteResponse = await _httpClient.DeleteAsync(
            $"/api/v1/collections/{_collectionId}",
            cancellationToken
        );

        _collectionId = null;
        _count = 0;

        await EnsureCollectionAsync(cancellationToken);

        _logger.LogInformation("Cleared Chroma collection {Collection}", _options.CollectionName);
    }

    /// <summary>
    /// 确保集合已创建
    /// </summary>
    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionId != null)
        {
            return;
        }

        // 尝试获取现有集合
        var getResponse = await _httpClient.GetAsync(
            $"/api/v1/collections/{_options.CollectionName}",
            cancellationToken
        );

        if (getResponse.IsSuccessStatusCode)
        {
            var collection = await getResponse.Content.ReadFromJsonAsync<ChromaCollection>(
                _jsonOptions,
                cancellationToken
            );

            if (collection != null)
            {
                _collectionId = collection.Id;
                _logger.LogDebug(
                    "Using existing Chroma collection {Collection} (id={Id})",
                    _options.CollectionName,
                    _collectionId
                );
                return;
            }
        }

        // 创建新集合
        var createRequest = new
        {
            name = _options.CollectionName,
            metadata = new Dictionary<string, object>
            {
                ["hnsw:space"] = _options.DistanceMetric.ToString().ToLowerInvariant(),
            },
        };

        var createResponse = await _httpClient.PostAsJsonAsync(
            "/api/v1/collections",
            createRequest,
            _jsonOptions,
            cancellationToken
        );

        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create Chroma collection: {createResponse.StatusCode} - {error}"
            );
        }

        var newCollection = await createResponse.Content.ReadFromJsonAsync<ChromaCollection>(
            _jsonOptions,
            cancellationToken
        );

        _collectionId = newCollection?.Id
            ?? throw new InvalidOperationException("Failed to get collection ID after creation");

        _logger.LogInformation(
            "Created Chroma collection {Collection} (id={Id})",
            _options.CollectionName,
            _collectionId
        );
    }

    /// <summary>
    /// 将距离转换为相似度分数
    /// </summary>
    private float ConvertDistanceToScore(float distance)
    {
        return _options.DistanceMetric switch
        {
            // 余弦距离：score = 1 - distance (distance 范围 0-2)
            ChromaDistanceMetric.Cosine => 1f - (distance / 2f),
            // L2 距离：score = 1 / (1 + distance)
            ChromaDistanceMetric.L2 => 1f / (1f + distance),
            // 内积：直接返回（已经是相似度）
            ChromaDistanceMetric.InnerProduct => distance,
            _ => 1f - distance,
        };
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

#region Chroma API Models

internal class ChromaCollection
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal class ChromaQueryResponse
{
    [JsonPropertyName("ids")]
    public List<List<string>>? Ids { get; set; }

    [JsonPropertyName("distances")]
    public List<List<float>>? Distances { get; set; }

    [JsonPropertyName("documents")]
    public List<List<string>>? Documents { get; set; }

    [JsonPropertyName("metadatas")]
    public List<List<Dictionary<string, object?>>>? Metadatas { get; set; }

    [JsonPropertyName("embeddings")]
    public List<List<float[]>>? Embeddings { get; set; }
}

internal class ChromaGetResponse
{
    [JsonPropertyName("ids")]
    public List<string>? Ids { get; set; }

    [JsonPropertyName("documents")]
    public List<string>? Documents { get; set; }

    [JsonPropertyName("metadatas")]
    public List<Dictionary<string, object?>>? Metadatas { get; set; }

    [JsonPropertyName("embeddings")]
    public List<float[]>? Embeddings { get; set; }
}

#endregion
