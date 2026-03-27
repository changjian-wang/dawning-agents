using System.ClientModel;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Embeddings;

namespace Dawning.Agents.OpenAI;

/// <summary>
/// OpenAI embedding provider implementation.
/// </summary>
/// <remarks>
/// Supports the following OpenAI text-embedding models:
/// <list type="bullet">
///   <item>text-embedding-3-small (1536 dimensions, recommended)</item>
///   <item>text-embedding-3-large (3072 dimensions)</item>
///   <item>text-embedding-ada-002 (1536 dimensions, legacy)</item>
/// </list>
///
/// Configuration example:
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
    private readonly ILogger<OpenAIEmbeddingProvider> _logger;

    /// <summary>
    /// Model dimension mapping.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> s_modelDimensions = new Dictionary<
        string,
        int
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["text-embedding-3-small"] = 1536,
        ["text-embedding-3-large"] = 3072,
        ["text-embedding-ada-002"] = 1536,
    };

    public string Name => "OpenAI";

    public int Dimensions => _dimensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The embedding model name. Defaults to <c>text-embedding-3-small</c>.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAIEmbeddingProvider(
        string apiKey,
        string model = "text-embedding-3-small",
        ILogger<OpenAIEmbeddingProvider>? logger = null
    )
        : this(apiKey, model, null, logger) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIEmbeddingProvider"/> class with a custom endpoint.
    /// Use this constructor for OpenAI-compatible embedding endpoints.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="model">The embedding model name.</param>
    /// <param name="endpoint">The base URL of the OpenAI-compatible API, or <c>null</c> for the default OpenAI endpoint.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAIEmbeddingProvider(
        string apiKey,
        string model,
        string? endpoint,
        ILogger<OpenAIEmbeddingProvider>? logger = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _model = model;
        _dimensions = GetModelDimensions(model);
        _logger = logger ?? NullLogger<OpenAIEmbeddingProvider>.Instance;

        OpenAIClient client;
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
            client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        }
        else
        {
            client = new OpenAIClient(apiKey);
        }

        _embeddingClient = client.GetEmbeddingClient(model);
        _logger.LogDebug(
            "OpenAIEmbeddingProvider created, model: {Model}, dimensions: {Dimensions}",
            model,
            _dimensions
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIEmbeddingProvider"/> class
    /// with a custom <see cref="EmbeddingClient"/> for testing.
    /// </summary>
    internal OpenAIEmbeddingProvider(EmbeddingClient embeddingClient, string model, int dimensions)
    {
        ArgumentNullException.ThrowIfNull(embeddingClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _embeddingClient = embeddingClient;
        _model = model;
        _dimensions = dimensions;
        _logger = NullLogger<OpenAIEmbeddingProvider>.Instance;
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

        // Filter empty texts and record original indices
        var validTexts = new List<(int Index, string Text)>();
        for (int i = 0; i < textList.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(textList[i]))
            {
                validTexts.Add((i, textList[i]));
            }
        }

        // All texts are empty
        if (validTexts.Count == 0)
        {
            return textList.Select(_ => new float[_dimensions]).ToList();
        }

        // Batch API call
        var result = await _embeddingClient
            .GenerateEmbeddingsAsync(
                validTexts.Select(v => v.Text).ToList(),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);

        // Build result array
        var embeddings = new float[textList.Count][];
        for (int i = 0; i < textList.Count; i++)
        {
            embeddings[i] = new float[_dimensions];
        }

        // Fill in valid results
        for (int i = 0; i < validTexts.Count; i++)
        {
            embeddings[validTexts[i].Index] = result.Value[i].ToFloats().ToArray();
        }

        return embeddings;
    }

    /// <summary>
    /// Gets the vector dimensions for the specified model.
    /// </summary>
    private static int GetModelDimensions(string model)
    {
        if (s_modelDimensions.TryGetValue(model, out var dimensions))
        {
            return dimensions;
        }

        // Default to 1536 dimensions for unknown models
        return 1536;
    }
}
