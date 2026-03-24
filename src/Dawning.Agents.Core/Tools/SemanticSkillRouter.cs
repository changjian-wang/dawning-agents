using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 语义技能路由器 — 基于向量相似度检索最相关的工具
/// </summary>
public sealed class SemanticSkillRouter : ISkillRouter
{
    private const string _toolCollectionPrefix = "dawning-skill-router-";
    private readonly IToolReader _toolReader;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly SkillRouterOptions _options;
    private readonly ILogger<SemanticSkillRouter> _logger;
    private volatile bool _indexBuilt;

    /// <summary>
    /// 创建语义技能路由器
    /// </summary>
    public SemanticSkillRouter(
        IToolReader toolReader,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IOptions<SkillRouterOptions> options,
        ILogger<SemanticSkillRouter>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(toolReader);
        ArgumentNullException.ThrowIfNull(embeddingProvider);
        ArgumentNullException.ThrowIfNull(vectorStore);
        ArgumentNullException.ThrowIfNull(options);

        _toolReader = toolReader;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _options = options.Value;
        _logger = logger ?? NullLogger<SemanticSkillRouter>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoredTool>> RouteAsync(
        string taskDescription,
        int topK = 5,
        float minScore = 0.3f,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskDescription);

        var allTools = _toolReader.GetAllTools();

        // 工具数不足阈值，全量返回（无需语义检索）
        if (allTools.Count < _options.ActivationThreshold)
        {
            _logger.LogDebug(
                "Tool count {Count} below activation threshold {Threshold}, returning all tools",
                allTools.Count,
                _options.ActivationThreshold
            );
            return allTools.Select(t => new ScoredTool(t, 1.0f)).ToList();
        }

        // 延迟构建索引
        if (!_indexBuilt)
        {
            await RebuildIndexAsync(cancellationToken).ConfigureAwait(false);
        }

        // 语义检索
        var queryEmbedding = await _embeddingProvider
            .EmbedAsync(taskDescription, cancellationToken)
            .ConfigureAwait(false);

        var searchResults = await _vectorStore
            .SearchAsync(queryEmbedding, topK, minScore, cancellationToken)
            .ConfigureAwait(false);

        var toolMap = allTools.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        var result = new List<ScoredTool>();
        foreach (var sr in searchResults)
        {
            if (
                sr.Chunk.Metadata.TryGetValue("tool_name", out var toolName)
                && toolMap.TryGetValue(toolName, out var tool)
            )
            {
                result.Add(new ScoredTool(tool, sr.Score));
            }
        }

        _logger.LogDebug(
            "Routed task to {Count} tools (top-K={TopK}, min={MinScore})",
            result.Count,
            topK,
            minScore
        );

        return result;
    }

    /// <inheritdoc />
    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        var tools = _toolReader.GetAllTools();
        _logger.LogInformation("Rebuilding skill router index for {Count} tools", tools.Count);

        var texts = tools.Select(BuildToolText).ToList();
        var embeddings = await _embeddingProvider
            .EmbedBatchAsync(texts, cancellationToken)
            .ConfigureAwait(false);

        var chunks = tools
            .Zip(embeddings)
            .Select(
                (pair, i) =>
                    new DocumentChunk
                    {
                        Id = $"{_toolCollectionPrefix}{pair.First.Name}",
                        Content = texts[i],
                        Embedding = pair.Second,
                        Metadata = new Dictionary<string, string>
                        {
                            ["tool_name"] = pair.First.Name,
                        },
                    }
            );

        await _vectorStore.AddBatchAsync(chunks, cancellationToken).ConfigureAwait(false);
        _indexBuilt = true;

        _logger.LogInformation("Skill router index built with {Count} tools", tools.Count);
    }

    private static string BuildToolText(ITool tool)
    {
        var text = $"{tool.Name}: {tool.Description}";

        if (tool is Core.EphemeralTool ephemeral && ephemeral.Definition.Metadata.WhenToUse != null)
        {
            text += $"\nWhen to use: {ephemeral.Definition.Metadata.WhenToUse}";
        }

        return text;
    }
}
