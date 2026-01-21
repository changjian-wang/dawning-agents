namespace Dawning.Agents.Abstractions.RAG;

/// <summary>
/// 嵌入向量提供者接口
/// </summary>
/// <remarks>
/// 将文本转换为向量表示，用于语义搜索和相似度计算。
/// </remarks>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 向量维度
    /// </summary>
    int Dimensions { get; }

    /// <summary>
    /// 生成单个文本的嵌入向量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入向量</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成文本的嵌入向量
    /// </summary>
    /// <param name="texts">输入文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入向量列表</returns>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    );
}
