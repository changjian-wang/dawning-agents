using System.Security.Cryptography;
using System.Text;
using Dawning.Agents.Abstractions.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.RAG;

/// <summary>
/// Simple embedding provider using character-based local vectorization.
/// </summary>
/// <remarks>
/// <para>This is a simplified local implementation for testing and demonstration purposes.</para>
/// <para>Production environments should use OpenAI/Azure embedding APIs.</para>
/// <para>
/// Implementation details:
/// <list type="number">
///   <item>Convert text to lowercase and tokenize.</item>
///   <item>Compute a hash for each token.</item>
///   <item>Map hash values into the vector space.</item>
///   <item>Normalize the vector.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class SimpleEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;
    private readonly ILogger<SimpleEmbeddingProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="dimensions">The vector dimensions (default: 384).</param>
    /// <param name="logger">The logger instance.</param>
    public SimpleEmbeddingProvider(
        int dimensions = 384,
        ILogger<SimpleEmbeddingProvider>? logger = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dimensions, 0);
        _dimensions = dimensions;
        _logger = logger ?? NullLogger<SimpleEmbeddingProvider>.Instance;
    }

    /// <inheritdoc />
    public string Name => "SimpleEmbedding";

    /// <inheritdoc />
    public int Dimensions => _dimensions;

    /// <inheritdoc />
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new float[_dimensions]);
        }

        var embedding = ComputeEmbedding(text);
        return Task.FromResult(embedding);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default
    )
    {
        var embeddings = texts.Select(ComputeEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    /// <summary>
    /// Computes the embedding vector for the given text.
    /// </summary>
    private float[] ComputeEmbedding(string text)
    {
        var vector = new float[_dimensions];

        // Preprocess text
        var normalizedText = text.ToLowerInvariant();
        var tokens = Tokenize(normalizedText);

        if (tokens.Count == 0)
        {
            return vector;
        }

        // Compute features for each token and accumulate into the vector
        foreach (var token in tokens)
        {
            var tokenHash = ComputeHash(token);

            // Map different portions of the hash to different vector positions
            for (var i = 0; i < Math.Min(tokenHash.Length * 8, _dimensions); i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;

                if (byteIndex < tokenHash.Length)
                {
                    var bit = (tokenHash[byteIndex] >> bitIndex) & 1;
                    vector[i % _dimensions] += bit == 1 ? 1f : -1f;
                }
            }

            // Add n-gram features
            for (var n = 2; n <= Math.Min(3, token.Length); n++)
            {
                for (var j = 0; j <= token.Length - n; j++)
                {
                    var ngram = token.Substring(j, n);
                    var ngramHash = ComputeHash(ngram);
                    var index = (BitConverter.ToInt32(ngramHash, 0) & 0x7FFFFFFF) % _dimensions;
                    vector[index] += 0.5f;
                }
            }
        }

        // Normalize vector
        Normalize(vector);

        _logger.LogDebug(
            "Computed embedding for text ({Length} chars, {Tokens} tokens)",
            text.Length,
            tokens.Count
        );

        return vector;
    }

    /// <summary>
    /// Tokenizes the input text.
    /// </summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c > 127) // Include CJK characters
            {
                currentToken.Append(c);
            }
            else if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string.
    /// </summary>
    private static byte[] ComputeHash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>
    /// Normalizes the vector using L2 norm.
    /// </summary>
    private static void Normalize(float[] vector)
    {
        var norm = 0f;
        foreach (var v in vector)
        {
            norm += v * v;
        }

        norm = MathF.Sqrt(norm);

        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }
}
