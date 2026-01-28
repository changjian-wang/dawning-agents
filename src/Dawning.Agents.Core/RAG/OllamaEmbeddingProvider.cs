using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Ollama Embedding 提供者
/// </summary>
/// <remarks>
/// 使用本地 Ollama 服务生成嵌入向量。
///
/// 支持的模型：
/// <list type="bullet">
///   <item>nomic-embed-text (768 维，推荐)</item>
///   <item>mxbai-embed-large (1024 维)</item>
///   <item>all-minilm (384 维)</item>
/// </list>
///
/// 配置示例:
/// <code>
/// {
///   "RAG": {
///     "EmbeddingModel": "nomic-embed-text"
///   },
///   "LLM": {
///     "Endpoint": "http://localhost:11434"
///   }
/// }
/// </code>
///
/// 安装模型: ollama pull nomic-embed-text
/// </remarks>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly int _dimensions;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    /// <summary>
    /// 模型维度映射
    /// </summary>
    private static readonly Dictionary<string, int> ModelDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nomic-embed-text"] = 768,
        ["mxbai-embed-large"] = 1024,
        ["all-minilm"] = 384,
        ["snowflake-arctic-embed"] = 1024,
        ["bge-m3"] = 1024,
    };

    public string Name => "Ollama";

    public int Dimensions => _dimensions;

    /// <summary>
    /// 创建 Ollama Embedding Provider
    /// </summary>
    /// <param name="httpClient">HTTP 客户端（BaseAddress 应指向 Ollama 端点）</param>
    /// <param name="model">嵌入模型名称（默认 nomic-embed-text）</param>
    /// <param name="logger">日志记录器</param>
    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        string model = "nomic-embed-text",
        ILogger<OllamaEmbeddingProvider>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _httpClient = httpClient;
        _model = model;
        _dimensions = GetModelDimensions(model);
        _logger = logger ?? NullLogger<OllamaEmbeddingProvider>.Instance;

        _logger.LogDebug("OllamaEmbeddingProvider 已创建，模型: {Model}，维度: {Dimensions}", model, _dimensions);
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

        var request = new OllamaEmbedRequest
        {
            Model = _model,
            Input = text,
        };

        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("发送嵌入请求到 Ollama，模型: {Model}", _model);

        var response = await _httpClient.PostAsync("/api/embed", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(
            JsonOptions.Default,
            cancellationToken
        );

        if (result?.Embeddings is null || result.Embeddings.Count == 0)
        {
            _logger.LogWarning("Ollama 返回空嵌入结果");
            return new float[_dimensions];
        }

        return result.Embeddings[0];
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

        // Ollama 的 /api/embed 支持批量输入
        var validTexts = new List<(int Index, string Text)>();
        for (int i = 0; i < textList.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(textList[i]))
            {
                validTexts.Add((i, textList[i]));
            }
        }

        if (validTexts.Count == 0)
        {
            return textList.Select(_ => new float[_dimensions]).ToList();
        }

        var request = new OllamaEmbedBatchRequest
        {
            Model = _model,
            Input = validTexts.Select(v => v.Text).ToList(),
        };

        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("发送批量嵌入请求到 Ollama，模型: {Model}，数量: {Count}", _model, validTexts.Count);

        var response = await _httpClient.PostAsync("/api/embed", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(
            JsonOptions.Default,
            cancellationToken
        );

        // 构建结果数组
        var embeddings = new float[textList.Count][];
        for (int i = 0; i < textList.Count; i++)
        {
            embeddings[i] = new float[_dimensions];
        }

        // 填充有效结果
        if (result?.Embeddings is not null)
        {
            for (int i = 0; i < validTexts.Count && i < result.Embeddings.Count; i++)
            {
                embeddings[validTexts[i].Index] = result.Embeddings[i];
            }
        }

        return embeddings;
    }

    /// <summary>
    /// 获取模型的向量维度
    /// </summary>
    private static int GetModelDimensions(string model)
    {
        // 移除版本标签（如 nomic-embed-text:latest）
        var baseName = model.Split(':')[0];

        if (ModelDimensions.TryGetValue(baseName, out var dimensions))
        {
            return dimensions;
        }

        // 未知模型默认使用 768 维
        return 768;
    }

    #region Ollama API Models

    private sealed class OllamaEmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private sealed class OllamaEmbedBatchRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required List<string> Input { get; init; }
    }

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; init; }
    }

    private static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    #endregion
}
