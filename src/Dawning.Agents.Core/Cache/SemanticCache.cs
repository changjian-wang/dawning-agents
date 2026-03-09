using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Cache;

/// <summary>
/// 语义缓存实现 - 基于向量相似度的智能缓存
/// </summary>
/// <remarks>
/// <para>使用向量存储和 Embedding 实现语义级别的缓存</para>
/// <para>当新查询与缓存中的查询相似度超过阈值时，直接返回缓存响应</para>
/// <para>线程安全</para>
/// </remarks>
public class SemanticCache : ISemanticCache
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly SemanticCacheOptions _options;
    private readonly ILogger<SemanticCache> _logger;
    private readonly string _namespace;

    /// <summary>
    /// 获取缓存条目数量
    /// </summary>
    public int Count => _vectorStore.Count;

    /// <summary>
    /// 初始化语义缓存
    /// </summary>
    /// <param name="vectorStore">向量存储</param>
    /// <param name="embeddingProvider">嵌入提供者</param>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
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
        _options = options?.Value ?? new SemanticCacheOptions();
        _logger = logger ?? NullLogger<SemanticCache>.Instance;
        _namespace = _options.Namespace;

        _logger.LogDebug(
            "SemanticCache 初始化，命名空间: {Namespace}，相似度阈值: {Threshold}",
            _namespace,
            _options.SimilarityThreshold
        );
    }

    /// <summary>
    /// 尝试获取语义相似的缓存响应
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
            // 生成查询嵌入
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // 搜索相似的缓存条目
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
                _logger.LogDebug("缓存未命中: {Query}", TruncateQuery(query));
                return null;
            }

            var bestMatch = results[0];
            var chunk = bestMatch.Chunk;

            // 检查是否过期
            if (IsExpired(chunk))
            {
                _logger.LogDebug("缓存已过期: {Id}", chunk.Id);
                await _vectorStore.DeleteAsync(chunk.Id, cancellationToken).ConfigureAwait(false);
                return null;
            }

            _logger.LogDebug(
                "缓存命中: 相似度={Score:F3}, 原始查询={OriginalQuery}",
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
                    .Metadata.Where(kv => !kv.Key.StartsWith("_"))
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "语义缓存查询失败");
            return null;
        }
    }

    /// <summary>
    /// 存储查询和响应到缓存
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
            // 检查是否需要淘汰旧条目
            if (_vectorStore.Count >= _options.MaxEntries)
            {
                _logger.LogWarning(
                    "缓存已满 ({Count}/{Max})，跳过添加",
                    _vectorStore.Count,
                    _options.MaxEntries
                );
                return;
            }

            // 生成查询嵌入
            var queryEmbedding = await _embeddingProvider
                .EmbedAsync(query, cancellationToken)
                .ConfigureAwait(false);

            // 构建元数据
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

            // 创建缓存条目
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
                "缓存已添加: {Id}, 查询长度={QueryLen}, 响应长度={ResponseLen}",
                chunk.Id,
                query.Length,
                response.Length
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "语义缓存存储失败");
        }
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _vectorStore
                .DeleteByDocumentIdAsync(_namespace, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("语义缓存已清除，命名空间: {Namespace}", _namespace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清除语义缓存失败");
        }
    }

    /// <summary>
    /// 检查缓存条目是否过期
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
    /// 截断查询用于日志
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
