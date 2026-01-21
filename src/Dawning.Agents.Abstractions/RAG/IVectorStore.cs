namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// 文档块 - 存储在向量数据库中的最小单元
/// </summary>
public record DocumentChunk
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 文本内容
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 嵌入向量
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    /// 元数据（来源文件、页码等）
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>
    /// 所属文档 ID
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// 在文档中的块索引
    /// </summary>
    public int ChunkIndex { get; init; }
}

/// <summary>
/// 搜索结果
/// </summary>
public record SearchResult
{
    /// <summary>
    /// 文档块
    /// </summary>
    public required DocumentChunk Chunk { get; init; }

    /// <summary>
    /// 相似度分数 (0-1，越高越相似)
    /// </summary>
    public required float Score { get; init; }
}

/// <summary>
/// 向量存储接口
/// </summary>
/// <remarks>
/// 提供向量的存储、检索和删除功能。
/// </remarks>
public interface IVectorStore
{
    /// <summary>
    /// 存储名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 存储的文档块数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 添加文档块
    /// </summary>
    /// <param name="chunk">文档块（需包含嵌入向量）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加文档块
    /// </summary>
    /// <param name="chunks">文档块列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddBatchAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 通过向量搜索相似文档
    /// </summary>
    /// <param name="queryEmbedding">查询向量</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="minScore">最小相似度阈值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果列表（按相似度降序）</returns>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 删除文档块
    /// </summary>
    /// <param name="id">文档块 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某个文档的所有块
    /// </summary>
    /// <param name="documentId">文档 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的块数量</returns>
    Task<int> DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清空所有数据
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文档块
    /// </summary>
    /// <param name="id">文档块 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文档块，如果不存在返回 null</returns>
    Task<DocumentChunk?> GetAsync(string id, CancellationToken cancellationToken = default);
}
