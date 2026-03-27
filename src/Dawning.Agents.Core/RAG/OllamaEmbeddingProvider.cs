using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Ollama embedding provider.
/// </summary>
/// <remarks>
/// Generates embedding vectors using the local Ollama service.
///
/// Supported models:
/// <list type="bullet">
///   <item>nomic-embed-text (768 dimensions, recommended)</item>
///   <item>mxbai-embed-large (1024 dimensions)</item>
///   <item>all-minilm (384 dimensions)</item>
/// </list>
///
/// Configuration example:
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
/// Install model: <c>ollama pull nomic-embed-text</c>
/// </remarks>
public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly int _dimensions;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    /// <summary>
    /// Model-to-dimension mapping.
    /// </summary>
    private static readonly Dictionary<string, int> s_modelDimensions = new(
        StringComparer.OrdinalIgnoreCase
    )
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
    /// Initializes a new instance of the <see cref="OllamaEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client (BaseAddress should point to the Ollama endpoint).</param>
    /// <param name="model">The embedding model name (default: nomic-embed-text).</param>
    /// <param name="logger">The logger instance.</param>
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

        _logger.LogDebug(
            "OllamaEmbeddingProvider created, model: {Model}, dimensions: {Dimensions}",
            model,
            _dimensions
        );
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

        var request = new OllamaEmbedRequest { Model = _model, Input = text };

        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending embedding request to Ollama, model: {Model}", _model);

        using var response = await _httpClient
            .PostAsync("/api/embed", content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.LogError(
                "Ollama embedding request failed: {StatusCode} {Error}",
                response.StatusCode,
                errorBody
            );
            response.EnsureSuccessStatusCode();
        }

        var result = await response
            .Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        if (result?.Embeddings is null || result.Embeddings.Count == 0)
        {
            _logger.LogWarning("Ollama returned empty embedding result");
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

        // Ollama's /api/embed supports batch input
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

        _logger.LogDebug(
            "Sending batch embedding request to Ollama, model: {Model}, count: {Count}",
            _model,
            validTexts.Count
        );

        using var response = await _httpClient
            .PostAsync("/api/embed", content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response
                .Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.LogError(
                "Ollama batch embedding request failed: {StatusCode} {Error}",
                response.StatusCode,
                errorBody
            );
            response.EnsureSuccessStatusCode();
        }

        var result = await response
            .Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        // Build result array
        var embeddings = new float[textList.Count][];
        for (int i = 0; i < textList.Count; i++)
        {
            embeddings[i] = new float[_dimensions];
        }

        // Fill in valid results
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
    /// Gets the vector dimensions for the specified model.
    /// </summary>
    private static int GetModelDimensions(string model)
    {
        // Strip version tag (e.g. nomic-embed-text:latest)
        var baseName = model.Split(':')[0];

        if (s_modelDimensions.TryGetValue(baseName, out var dimensions))
        {
            return dimensions;
        }

        // Default to 768 dimensions for unknown models
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
