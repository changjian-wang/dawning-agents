using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// 知识库 - 整合文档管理、分块、嵌入和检索
/// </summary>
/// <remarks>
/// 提供端到端的 RAG 工作流：
/// 1. 添加文档 → 自动分块 → 生成嵌入 → 存储
/// 2. 查询 → 语义检索 → 返回相关内容
/// </remarks>
public sealed class KnowledgeBase
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunker _chunker;
    private readonly RAGOptions _options;
    private readonly ILogger<KnowledgeBase> _logger;

    /// <summary>
    /// 创建知识库
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
    /// 知识库名称
    /// </summary>
    public string Name => $"KnowledgeBase({_vectorStore.Name})";

    /// <summary>
    /// 文档块数量
    /// </summary>
    public int ChunkCount => _vectorStore.Count;

    /// <summary>
    /// 添加文档到知识库
    /// </summary>
    /// <param name="content">文档内容</param>
    /// <param name="documentId">文档 ID（可选，自动生成）</param>
    /// <param name="metadata">元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>添加的块数量</returns>
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

        // 1. 分块
        var chunks = _chunker.ChunkText(content, documentId, metadata);

        if (chunks.Count == 0)
        {
            return 0;
        }

        // 2. 批量生成嵌入
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingProvider.EmbedBatchAsync(texts, cancellationToken);

        // 3. 添加嵌入向量到块
        var chunksWithEmbeddings = chunks
            .Zip(embeddings, (chunk, embedding) => chunk with { Embedding = embedding })
            .ToList();

        // 4. 存储到向量数据库
        await _vectorStore.AddBatchAsync(chunksWithEmbeddings, cancellationToken);

        _logger.LogInformation(
            "Added document {DocumentId} with {ChunkCount} chunks to knowledge base",
            documentId,
            chunks.Count
        );

        return chunks.Count;
    }

    /// <summary>
    /// 从文件添加文档
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

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var documentId = Path.GetFileName(filePath);
        var metadata = new Dictionary<string, string>
        {
            ["source"] = filePath,
            ["filename"] = Path.GetFileName(filePath),
            ["extension"] = Path.GetExtension(filePath),
        };

        return await AddDocumentAsync(content, documentId, metadata, cancellationToken);
    }

    /// <summary>
    /// 查询知识库
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果</returns>
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

        // 生成查询向量
        var queryEmbedding = await _embeddingProvider.EmbedAsync(query, cancellationToken);

        // 向量搜索
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            _options.MinScore,
            cancellationToken
        );

        _logger.LogDebug("Query returned {Count} results", results.Count);

        return results;
    }

    /// <summary>
    /// 查询并返回格式化的上下文
    /// </summary>
    public async Task<string> QueryContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        var results = await QueryAsync(query, topK, cancellationToken);

        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n\n",
            results.Select((r, i) => $"[{i + 1}] (相似度: {r.Score:F2})\n{r.Chunk.Content}")
        );
    }

    /// <summary>
    /// 删除文档
    /// </summary>
    public Task<int> DeleteDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default
    )
    {
        return _vectorStore.DeleteByDocumentIdAsync(documentId, cancellationToken);
    }

    /// <summary>
    /// 清空知识库
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        return _vectorStore.ClearAsync(cancellationToken);
    }
}
