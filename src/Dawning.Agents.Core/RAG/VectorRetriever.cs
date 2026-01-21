using System.Text;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// 向量检索器 - 结合 Embedding 和 VectorStore 提供语义搜索
/// </summary>
public class VectorRetriever : IRetriever
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly RAGOptions _options;
    private readonly ILogger<VectorRetriever> _logger;

    /// <summary>
    /// 创建向量检索器
    /// </summary>
    public VectorRetriever(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IOptions<RAGOptions>? options = null,
        ILogger<VectorRetriever>? logger = null
    )
    {
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _options = options?.Value ?? new RAGOptions();
        _logger = logger ?? NullLogger<VectorRetriever>.Instance;
    }

    /// <inheritdoc />
    public string Name => $"VectorRetriever({_embeddingProvider.Name}, {_vectorStore.Name})";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        float minScore = 0.0f,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        _logger.LogDebug("Retrieving for query: {Query}", query);

        // 生成查询向量
        var queryEmbedding = await _embeddingProvider.EmbedAsync(query, cancellationToken);

        // 向量搜索
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            minScore,
            cancellationToken
        );

        _logger.LogDebug("Retrieved {Count} results for query", results.Count);

        return results;
    }

    /// <inheritdoc />
    public async Task<string> RetrieveContextAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        var results = await RetrieveAsync(
            query,
            topK,
            _options.MinScore,
            cancellationToken
        );

        if (results.Count == 0)
        {
            return string.Empty;
        }

        var contextBuilder = new StringBuilder();

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var context = _options.ContextTemplate
                .Replace("{index}", (i + 1).ToString())
                .Replace("{content}", result.Chunk.Content)
                .Replace("{score}", result.Score.ToString("F2"))
                .Replace("{source}", result.Chunk.Metadata.GetValueOrDefault("source", "unknown"));

            contextBuilder.AppendLine(context);

            if (_options.IncludeMetadata && result.Chunk.Metadata.Count > 0)
            {
                contextBuilder.AppendLine($"   Source: {string.Join(", ", result.Chunk.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }

            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString().Trim();
    }
}
