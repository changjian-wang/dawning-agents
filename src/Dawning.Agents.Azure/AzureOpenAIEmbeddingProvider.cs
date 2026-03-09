using Azure;
using Azure.AI.OpenAI;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Embeddings;

namespace Dawning.Agents.Azure;

/// <summary>
/// Azure OpenAI Embedding 提供者
/// </summary>
/// <remarks>
/// 支持 Azure OpenAI Service 部署的嵌入模型。
///
/// 配置示例:
/// <code>
/// {
///   "LLM": {
///     "Endpoint": "https://your-resource.openai.azure.com",
///     "ApiKey": "your-api-key"
///   },
///   "RAG": {
///     "EmbeddingModel": "text-embedding-deployment"  // 部署名称
///   }
/// }
/// </code>
/// </remarks>
public sealed class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly string _deploymentName;
    private readonly int _dimensions;
    private readonly ILogger<AzureOpenAIEmbeddingProvider> _logger;

    public string Name => "AzureOpenAI";

    public int Dimensions => _dimensions;

    /// <summary>
    /// 创建 Azure OpenAI Embedding Provider
    /// </summary>
    /// <param name="endpoint">Azure OpenAI 端点 URL</param>
    /// <param name="apiKey">Azure OpenAI API Key</param>
    /// <param name="deploymentName">嵌入模型部署名称</param>
    /// <param name="dimensions">向量维度（默认 1536）</param>
    /// <param name="logger">日志记录器</param>
    public AzureOpenAIEmbeddingProvider(
        string endpoint,
        string apiKey,
        string deploymentName,
        int dimensions = 1536,
        ILogger<AzureOpenAIEmbeddingProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        _deploymentName = deploymentName;
        _dimensions = dimensions;
        _logger = logger ?? NullLogger<AzureOpenAIEmbeddingProvider>.Instance;

        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _embeddingClient = client.GetEmbeddingClient(deploymentName);
        _logger.LogDebug(
            "AzureOpenAIEmbeddingProvider 已创建，端点: {Endpoint}，部署: {Deployment}，维度: {Dimensions}",
            endpoint,
            deploymentName,
            dimensions
        );
    }

    /// <summary>
    /// 使用自定义 EmbeddingClient 创建 Provider（用于测试）
    /// </summary>
    internal AzureOpenAIEmbeddingProvider(
        EmbeddingClient embeddingClient,
        string deploymentName,
        int dimensions
    )
    {
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        _embeddingClient = embeddingClient;
        _deploymentName = deploymentName;
        _dimensions = dimensions;
        _logger = NullLogger<AzureOpenAIEmbeddingProvider>.Instance;
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

        var result = await _embeddingClient
            .GenerateEmbeddingAsync(text, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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
        var result = await _embeddingClient
            .GenerateEmbeddingsAsync(
                validTexts.Select(v => v.Text).ToList(),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

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
}
