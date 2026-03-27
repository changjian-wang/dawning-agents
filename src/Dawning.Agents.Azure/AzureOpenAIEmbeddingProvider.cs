using Azure;
using Azure.AI.OpenAI;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Embeddings;

namespace Dawning.Agents.Azure;

/// <summary>
/// Azure OpenAI embedding provider implementation.
/// </summary>
/// <remarks>
/// Supports embedding models deployed on Azure OpenAI Service.
///
/// Configuration example:
/// <code>
/// {
///   "LLM": {
///     "Endpoint": "https://your-resource.openai.azure.com",
///     "ApiKey": "your-api-key"
///   },
///   "RAG": {
///     "EmbeddingModel": "text-embedding-deployment"  // Deployment name
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
    /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI endpoint URL.</param>
    /// <param name="apiKey">The Azure OpenAI API key.</param>
    /// <param name="deploymentName">The embedding model deployment name.</param>
    /// <param name="dimensions">The vector dimensions. Defaults to 1536.</param>
    /// <param name="logger">The logger instance.</param>
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
            "AzureOpenAIEmbeddingProvider created, endpoint: {Endpoint}, deployment: {Deployment}, dimensions: {Dimensions}",
            endpoint,
            deploymentName,
            dimensions
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIEmbeddingProvider"/> class
    /// with a custom <see cref="EmbeddingClient"/> for testing.
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
}
