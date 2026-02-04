namespace Dawning.Agents.Abstractions.Cache;

/// <summary>
/// 语义缓存接口 - 基于向量相似度的智能缓存
/// </summary>
/// <remarks>
/// <para>缓存 LLM 响应，当新查询与缓存中的查询语义相似时直接返回</para>
/// <para>可大幅减少重复 LLM 调用，降低成本和延迟</para>
/// </remarks>
public interface ISemanticCache
{
    /// <summary>
    /// 尝试获取语义相似的缓存响应
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存命中时返回响应，否则返回 null</returns>
    Task<SemanticCacheResult?> GetAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 存储查询和响应到缓存
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="response">响应文本</param>
    /// <param name="metadata">可选的元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync(
        string query,
        string response,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取缓存条目数量
    /// </summary>
    int Count { get; }
}

/// <summary>
/// 语义缓存结果
/// </summary>
public record SemanticCacheResult
{
    /// <summary>
    /// 缓存的响应内容
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// 原始查询文本
    /// </summary>
    public required string OriginalQuery { get; init; }

    /// <summary>
    /// 相似度分数 (0-1)
    /// </summary>
    public required float SimilarityScore { get; init; }

    /// <summary>
    /// 缓存创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}

/// <summary>
/// 语义缓存配置选项
/// </summary>
/// <remarks>
/// appsettings.json 示例:
/// <code>
/// {
///   "SemanticCache": {
///     "Enabled": true,
///     "SimilarityThreshold": 0.95,
///     "MaxEntries": 10000,
///     "ExpirationMinutes": 1440
///   }
/// }
/// </code>
/// </remarks>
public class SemanticCacheOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SemanticCache";

    /// <summary>
    /// 是否启用语义缓存
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 相似度阈值 (0-1)，超过此阈值才返回缓存
    /// </summary>
    /// <remarks>
    /// 默认 0.95，较高的阈值确保只返回高度相似的缓存
    /// </remarks>
    public float SimilarityThreshold { get; set; } = 0.95f;

    /// <summary>
    /// 最大缓存条目数
    /// </summary>
    public int MaxEntries { get; set; } = 10000;

    /// <summary>
    /// 缓存过期时间（分钟）
    /// </summary>
    public int ExpirationMinutes { get; set; } = 1440; // 24 小时

    /// <summary>
    /// 命名空间，用于隔离不同应用的缓存
    /// </summary>
    public string Namespace { get; set; } = "default";
}
