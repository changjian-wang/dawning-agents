using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Weaviate;

/// <summary>
/// Weaviate 向量存储实现
/// </summary>
/// <remarks>
/// Weaviate 是一个开源的向量搜索引擎，支持：
/// - GraphQL 和 REST API
/// - 多种向量索引类型（HNSW、Flat、Dynamic）
/// - 多租户
/// - 混合搜索（向量 + 关键词）
/// </remarks>
public partial class WeaviateVectorStore : IVectorStore, IAsyncDisposable
{
    private static readonly Regex ValidClassNameRegex = ClassNameRegex();
    private readonly HttpClient _httpClient;
    private readonly WeaviateOptions _options;
    private readonly ILogger<WeaviateVectorStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private int _count;
    private volatile bool _classEnsured;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*$")]
    private static partial Regex ClassNameRegex();

    /// <inheritdoc />
    public string Name => "Weaviate";

    /// <inheritdoc />
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// 创建 Weaviate 向量存储实例
    /// </summary>
    public WeaviateVectorStore(
        HttpClient httpClient,
        IOptions<WeaviateOptions> options,
        ILogger<WeaviateVectorStore>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();
        _logger = logger ?? NullLogger<WeaviateVectorStore>.Instance;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        if (!ValidClassNameRegex.IsMatch(_options.ClassName))
        {
            throw new ArgumentException(
                $"Invalid Weaviate ClassName '{_options.ClassName}'. Must match ^[a-zA-Z][a-zA-Z0-9_]*$",
                nameof(options)
            );
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);
        await AddBatchAsync([chunk], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);

        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Adding {Count} chunks to Weaviate", chunkList.Count);

        // 使用批量导入 API
        var objects = chunkList
            .Select(chunk => new WeaviateObject
            {
                Class = _options.ClassName,
                Id = chunk.Id,
                Vector = chunk.Embedding,
                Properties = new Dictionary<string, object?>
                {
                    ["content"] = chunk.Content,
                    ["documentId"] = chunk.DocumentId,
                    ["chunkIndex"] = chunk.ChunkIndex,
                    ["metadata"] = JsonSerializer.Serialize(chunk.Metadata, _jsonOptions),
                },
            })
            .ToList();

        var batchRequest = new WeaviateBatchRequest { Objects = objects };

        using var response = await _httpClient
            .PostAsJsonAsync("/v1/batch/objects", batchRequest, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            Interlocked.Add(ref _count, chunkList.Count);
            _logger.LogDebug("Successfully added {Count} chunks", chunkList.Count);
        }
        else
        {
            var error = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.LogError("Failed to add chunks to Weaviate: {Error}", error);
            throw new InvalidOperationException($"Failed to add chunks to Weaviate: {error}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        _logger.LogDebug(
            "Searching Weaviate with topK={TopK}, minScore={MinScore}",
            topK,
            minScore
        );

        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);

        // ClassName is validated in constructor via regex — safe for GraphQL interpolation
        // Embedding vector is serialized via JSON to prevent format injection
        var vectorJson = JsonSerializer.Serialize(queryEmbedding);
        var graphqlQuery = new WeaviateGraphQLQuery
        {
            Query = $$"""
                {
                    Get {
                        {{_options.ClassName}}(
                            nearVector: {
                                vector: {{vectorJson}}
                            }
                            limit: {{topK}}
                        ) {
                            _additional {
                                id
                                distance
                                certainty
                            }
                            content
                            documentId
                            chunkIndex
                            metadata
                        }
                    }
                }
                """,
        };

        using var response = await _httpClient
            .PostAsJsonAsync("/v1/graphql", graphqlQuery, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.LogError("Failed to search Weaviate: {Error}", error);
            return [];
        }

        var result = await response
            .Content.ReadFromJsonAsync<WeaviateGraphQLResponse>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result?.Data?.Get == null)
        {
            return [];
        }

        // 解析结果
        var results = new List<SearchResult>();

        if (
            result.Data.Get.TryGetValue(_options.ClassName, out var classData)
            && classData is JsonElement jsonArray
        )
        {
            if (jsonArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "搜索结果类型异常，期望数组，实际为 {ValueKind}",
                    jsonArray.ValueKind
                );
                return results;
            }

            foreach (var item in jsonArray.EnumerateArray())
            {
                if (!item.TryGetProperty("_additional", out var additional))
                {
                    _logger.LogWarning("搜索结果缺少 _additional 字段，跳过");
                    continue;
                }

                var certainty =
                    additional.TryGetProperty("certainty", out var cert)
                    && cert.ValueKind == JsonValueKind.Number
                    && cert.TryGetSingle(out var certaintyValue)
                        ? certaintyValue
                        : 0f;

                // certainty 转换为 score (certainty 范围是 0-1)
                var score = certainty;

                if (score < minScore)
                {
                    continue;
                }

                var content = item.TryGetProperty("content", out var contentProp)
                    ? contentProp.GetString() ?? ""
                    : "";

                var documentId = item.TryGetProperty("documentId", out var docIdProp)
                    ? docIdProp.GetString()
                    : null;

                var chunkIndex = item.TryGetProperty("chunkIndex", out var indexProp)
                    ? indexProp.GetInt32()
                    : 0;

                var metadataJson = item.TryGetProperty("metadata", out var metaProp)
                    ? metaProp.GetString()
                    : null;

                var metadata = !string.IsNullOrEmpty(metadataJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(
                        metadataJson,
                        _jsonOptions
                    ) ?? []
                    : [];

                var id = additional.TryGetProperty("id", out var idProp)
                    ? idProp.GetString()
                    : null;

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogWarning("搜索结果缺少 _additional.id，跳过");
                    continue;
                }

                var chunk = new DocumentChunk
                {
                    Id = id,
                    Content = content,
                    DocumentId = documentId,
                    ChunkIndex = chunkIndex,
                    Metadata = metadata,
                };

                results.Add(new SearchResult { Chunk = chunk, Score = score });
            }
        }

        _logger.LogDebug("Found {Count} results", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task<DocumentChunk?> GetAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Getting chunk {Id} from Weaviate", id);

        using var response = await _httpClient
            .GetAsync(
                $"/v1/objects/{_options.ClassName}/{Uri.EscapeDataString(id)}",
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response
            .Content.ReadFromJsonAsync<WeaviateObject>(_jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (result?.Properties == null)
        {
            return null;
        }

        var content = result.Properties.TryGetValue("content", out var contentObj)
            ? contentObj?.ToString() ?? ""
            : "";

        var documentId = result.Properties.TryGetValue("documentId", out var docIdObj)
            ? docIdObj?.ToString()
            : null;

        var chunkIndex = result.Properties.TryGetValue("chunkIndex", out var indexObj)
            ? int.TryParse(indexObj?.ToString(), out var idx)
                ? idx
                : 0
            : 0;

        var metadataJson = result.Properties.TryGetValue("metadata", out var metaObj)
            ? metaObj?.ToString()
            : null;

        var metadata = !string.IsNullOrEmpty(metadataJson)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, _jsonOptions)
                ?? []
            : [];

        return new DocumentChunk
        {
            Id = id,
            Content = content,
            Embedding = result.Vector,
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Metadata = metadata,
        };
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Deleting chunk {Id} from Weaviate", id);

        using var response = await _httpClient
            .DeleteAsync(
                $"/v1/objects/{_options.ClassName}/{Uri.EscapeDataString(id)}",
                cancellationToken
            )
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            int oldCount;
            do
            {
                oldCount = Volatile.Read(ref _count);
                if (oldCount <= 0)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _count, oldCount - 1, oldCount) != oldCount);

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureClassExistsOnceAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Deleting chunks by documentId {DocumentId} from Weaviate", documentId);

        // 使用批量删除 API
        var deleteRequest = new WeaviateBatchDeleteRequest
        {
            Match = new WeaviateBatchDeleteMatch
            {
                Class = _options.ClassName,
                Where = new WeaviateWhereFilter
                {
                    Path = ["documentId"],
                    Operator = "Equal",
                    ValueText = documentId,
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/batch/objects")
        {
            Content = JsonContent.Create(deleteRequest, options: _jsonOptions),
        };

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var result = await response
                .Content.ReadFromJsonAsync<WeaviateBatchDeleteResponse>(
                    _jsonOptions,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (result?.Results?.Successful > 0)
            {
                var successful = result.Results.Successful;
                if (successful > int.MaxValue)
                {
                    _logger.LogWarning(
                        "Delete result successful count {Successful} exceeds Int32 range; clamping",
                        successful
                    );
                    successful = int.MaxValue;
                }

                var deleted = (int)successful;
                int oldCount;
                int newCount;
                do
                {
                    oldCount = Volatile.Read(ref _count);
                    newCount = oldCount <= deleted ? 0 : oldCount - deleted;
                } while (Interlocked.CompareExchange(ref _count, newCount, oldCount) != oldCount);

                return deleted;
            }
        }

        return 0;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Clearing all chunks from Weaviate class {ClassName}", _options.ClassName);

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 删除并重建 Schema 类
            using var deleteResponse = await _httpClient
                .DeleteAsync($"/v1/schema/{_options.ClassName}", cancellationToken)
                .ConfigureAwait(false);

            if (!deleteResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to delete Weaviate class '{_options.ClassName}': {deleteResponse.StatusCode}"
                );
            }

            _classEnsured = false;
            await EnsureClassExistsAsync(cancellationToken).ConfigureAwait(false);
            _classEnsured = true;

            Interlocked.Exchange(ref _count, 0);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        try
        {
            _initLock.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 确保 Schema 类已创建（懒初始化，仅执行一次）
    /// </summary>
    private async Task EnsureClassExistsOnceAsync(CancellationToken cancellationToken = default)
    {
        if (_classEnsured)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_classEnsured)
            {
                return;
            }

            await EnsureClassExistsAsync(cancellationToken).ConfigureAwait(false);
            _classEnsured = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 确保 Schema 类存在
    /// </summary>
    private async Task EnsureClassExistsAsync(CancellationToken cancellationToken = default)
    {
        // 检查类是否存在
        using var response = await _httpClient
            .GetAsync($"/v1/schema/{_options.ClassName}", cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Weaviate class {ClassName} already exists", _options.ClassName);
            return;
        }

        // 创建类
        var classSchema = new WeaviateClassSchema
        {
            Class = _options.ClassName,
            Description = "Document chunks for Dawning.Agents",
            VectorIndexType = _options.VectorIndexType.ToString().ToLowerInvariant(),
            VectorIndexConfig = new Dictionary<string, object>
            {
                ["distance"] = _options.DistanceMetric.ToString().ToLowerInvariant(),
            },
            Properties =
            [
                new WeaviateProperty
                {
                    Name = "content",
                    DataType = ["text"],
                    Description = "Chunk content",
                },
                new WeaviateProperty
                {
                    Name = "documentId",
                    DataType = ["text"],
                    Description = "Original document ID",
                },
                new WeaviateProperty
                {
                    Name = "chunkIndex",
                    DataType = ["int"],
                    Description = "Chunk index in document",
                },
                new WeaviateProperty
                {
                    Name = "metadata",
                    DataType = ["text"],
                    Description = "Chunk metadata as JSON",
                },
            ],
        };

        using var createResponse = await _httpClient
            .PostAsJsonAsync("/v1/schema", classSchema, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (createResponse.IsSuccessStatusCode)
        {
            _logger.LogInformation("Created Weaviate class {ClassName}", _options.ClassName);
        }
        else
        {
            var error = await createResponse
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Failed to create Weaviate class '{_options.ClassName}': [{(int)createResponse.StatusCode}] {error}"
            );
        }
    }
}

#region Weaviate API Models

internal class WeaviateObject
{
    public string Class { get; set; } = "";
    public string? Id { get; set; }
    public float[]? Vector { get; set; }
    public Dictionary<string, object?>? Properties { get; set; }
}

internal class WeaviateBatchRequest
{
    public List<WeaviateObject> Objects { get; set; } = [];
}

internal class WeaviateGraphQLQuery
{
    public string Query { get; set; } = "";
}

internal class WeaviateGraphQLResponse
{
    public WeaviateGraphQLData? Data { get; set; }
}

internal class WeaviateGraphQLData
{
    public Dictionary<string, object?>? Get { get; set; }
}

internal class WeaviateBatchDeleteRequest
{
    public WeaviateBatchDeleteMatch? Match { get; set; }
}

internal class WeaviateBatchDeleteMatch
{
    public string Class { get; set; } = "";
    public WeaviateWhereFilter? Where { get; set; }
}

internal class WeaviateWhereFilter
{
    public string[]? Path { get; set; }
    public string? Operator { get; set; }
    public string? ValueText { get; set; }
}

internal class WeaviateBatchDeleteResponse
{
    public WeaviateBatchDeleteResults? Results { get; set; }
}

internal class WeaviateBatchDeleteResults
{
    public long Successful { get; set; }
    public long Failed { get; set; }
}

internal class WeaviateClassSchema
{
    public string Class { get; set; } = "";
    public string? Description { get; set; }
    public string? VectorIndexType { get; set; }
    public Dictionary<string, object>? VectorIndexConfig { get; set; }
    public List<WeaviateProperty>? Properties { get; set; }
}

internal class WeaviateProperty
{
    public string Name { get; set; } = "";
    public string[]? DataType { get; set; }
    public string? Description { get; set; }
}

#endregion
