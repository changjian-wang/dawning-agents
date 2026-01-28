using System.ClientModel;
using Dawning.Agents.Abstractions.RAG;
using OpenAI;
using OpenAI.Embeddings;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// OpenAI Embedding 提供者
/// </summary>
/// <remarks>
/// 支持 OpenAI 的 text-embedding 系列模型：
/// <list type="bullet">
///   <item>text-embedding-3-small (1536 维，推荐)</item>
///   <item>text-embedding-3-large (3072 维)</item>
///   <item>text-embedding-ada-002 (1536 维，旧版)</item>
/// </list>
///
/// 配置示例:
/// <code>
/// {
///   "RAG": {
///     "EmbeddingModel": "text-embedding-3-small"
///   },
///   "LLM": {
///     "ApiKey": "sk-xxx"
///   }
/// }
/// </code>
/// </remarks>
public sealed class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly string _model;
    private readonly int _dimensions;

    /// <summary>
    /// 模型维度映射
    /// </summary>
    private static readonly Dictionary<string, int> ModelDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text-embedding-3-small"] = 1536,
        ["text-embedding-3-large"] = 3072,
        ["text-embedding-ada-002"] = 1536,
    };

    public string Name => "OpenAI";

    public int Dimensions => _dimensions;

    /// <summary>
    /// 创建 OpenAI Embedding Provider
    /// </summary>
    /// <param name="apiKey">OpenAI API Key</param>
    /// <param name="model">嵌入模型名称（默认 text-embedding-3-small）</param>
    public OpenAIEmbeddingProvider(string apiKey, string model = "text-embedding-3-small")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _model = model;
        _dimensions = GetModelDimensions(model);

        var client = new OpenAIClient(apiKey);
        _embeddingClient = client.GetEmbeddingClient(model);
    }

    /// <summary>
    /// 使用自定义 EmbeddingClient 创建 Provider（用于测试）
    /// </summary>
    internal OpenAIEmbeddingProvider(
        EmbeddingClient embeddingClient,
        string model,
        int dimensions
    )
    {
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _embeddingClient = embeddingClient;
        _model = model;
        _dimensions = dimensions;
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[_dimensions];
        }

        var result = await _embeddingClient.GenerateEmbeddingAsync(
            text,
            cancellationToken: cancellationToken
        );

        return result.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        // 过滤空文本，记录原始索引
        var validTexts = new List<(int Index, string Text)>();
        for (int i = 0; i < textList.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(textList[i]))
            {
                validTexts.Add((i, textList[i]));
            }
        }

        // 所有文本都为空
        if (validTexts.Count == 0)
        {
            return textList.Select(_ => new float[_dimensions]).ToList();
        }

        // 批量调用 API
        var result = await _embeddingClient.GenerateEmbeddingsAsync(
            validTexts.Select(v => v.Text).ToList(),
            cancellationToken: cancellationToken
        );

        // 构建结果数组
        var embeddings = new float[textList.Count][];
        for (int i = 0; i < textList.Count; i++)
        {
            embeddings[i] = new float[_dimensions];
        }

        // 填充有效结果
        for (int i = 0; i < validTexts.Count; i++)
        {
            embeddings[validTexts[i].Index] = result.Value[i].ToFloats().ToArray();
        }

        return embeddings;
    }

    /// <summary>
    /// 获取模型的向量维度
    /// </summary>
    private static int GetModelDimensions(string model)
    {
        if (ModelDimensions.TryGetValue(model, out var dimensions))
        {
            return dimensions;
        }

        // 未知模型默认使用 1536 维
        return 1536;
    }
}
