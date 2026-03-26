using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Cache;

/// <summary>
/// Defines a semantic cache that uses vector similarity to store and retrieve LLM responses.
/// </summary>
/// <remarks>
/// <para>Caches LLM responses and returns them when a new query is semantically similar to a cached query.</para>
/// <para>Significantly reduces redundant LLM calls, lowering both cost and latency.</para>
/// </remarks>
public interface ISemanticCache
{
    /// <summary>
    /// Attempts to retrieve a semantically similar cached response.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached result if a match is found; otherwise, <see langword="null"/>.</returns>
    Task<SemanticCacheResult?> GetAsync(
        string query,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Stores a query and its response in the cache.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="response">The response text.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task SetAsync(
        string query,
        string response,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Represents a semantic cache lookup result.
/// </summary>
public record SemanticCacheResult
{
    /// <summary>
    /// Gets the cached response content.
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Gets the original query text that produced the cached response.
    /// </summary>
    public required string OriginalQuery { get; init; }

    /// <summary>
    /// Gets the similarity score between the input query and the cached query (0–1).
    /// </summary>
    public required float SimilarityScore { get; init; }

    /// <summary>
    /// Gets the time the cache entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the metadata associated with the cached entry.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Configuration options for the semantic cache.
/// </summary>
/// <remarks>
/// appsettings.json example:
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
public class SemanticCacheOptions : IValidatableOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "SemanticCache";

    /// <summary>
    /// Gets or sets a value indicating whether the semantic cache is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the similarity threshold (0–1). Only results above this threshold are returned.
    /// </summary>
    /// <remarks>
    /// Defaults to 0.95. A higher threshold ensures only highly similar results are returned.
    /// </remarks>
    public float SimilarityThreshold { get; set; } = 0.95f;

    /// <summary>
    /// Gets or sets the maximum number of cache entries.
    /// </summary>
    public int MaxEntries { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the cache expiration time in minutes.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    /// Gets or sets the namespace used to isolate cache entries across applications.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <inheritdoc />
    public void Validate()
    {
        if (SimilarityThreshold is < 0f or > 1f)
        {
            throw new InvalidOperationException("SimilarityThreshold must be between 0.0 and 1.0");
        }

        if (MaxEntries <= 0)
        {
            throw new InvalidOperationException("MaxEntries must be greater than 0");
        }

        if (ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("ExpirationMinutes must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(Namespace))
        {
            throw new InvalidOperationException("Namespace is required");
        }
    }
}
