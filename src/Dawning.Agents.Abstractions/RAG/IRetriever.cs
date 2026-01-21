namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// 检索器接口 - 封装向量搜索和文本查询
/// </summary>
/// <remarks>
/// 检索器结合了 Embedding 和 VectorStore，提供端到端的语义搜索能力。
/// </remarks>
public interface IRetriever
{
    /// <summary>
    /// 检索器名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检索相关文档
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="minScore">最小相似度阈值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检索结果列表</returns>
    Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 检索并格式化为上下文字符串
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="topK">返回的最大结果数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>格式化的上下文字符串</returns>
    Task<string> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    );
}
