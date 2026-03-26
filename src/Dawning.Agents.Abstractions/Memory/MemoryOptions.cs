using Dawning.Agents.Abstractions;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Memory configuration options.
/// </summary>
/// <remarks>
/// appsettings.json example:
/// <code>
/// {
///   "Memory": {
///     "Type": "Vector",
///     "WindowSize": 10,
///     "MaxRecentMessages": 6,
///     "SummaryThreshold": 10,
///     "DowngradeThreshold": 4000,
///     "RetrieveTopK": 5,
///     "MinRelevanceScore": 0.5,
///     "ModelName": "gpt-4",
///     "MaxContextTokens": 8192
///   }
/// }
/// </code>
/// </remarks>
public class MemoryOptions : IValidatableOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Memory";

    /// <summary>
    /// Memory type: Buffer, Window, Summary, Adaptive, Vector.
    /// </summary>
    public MemoryType Type { get; set; } = MemoryType.Buffer;

    /// <summary>
    /// Window size (used by WindowMemory).
    /// </summary>
    public int WindowSize { get; set; } = 10;

    /// <summary>
    /// Number of recent messages to retain (used by SummaryMemory).
    /// </summary>
    public int MaxRecentMessages { get; set; } = 6;

    /// <summary>
    /// Message count threshold to trigger summarization (used by SummaryMemory).
    /// </summary>
    public int SummaryThreshold { get; set; } = 10;

    /// <summary>
    /// Token threshold to trigger downgrade (used by AdaptiveMemory).
    /// </summary>
    /// <remarks>
    /// When the token count in BufferMemory exceeds this threshold, it automatically downgrades to SummaryMemory.
    /// </remarks>
    public int DowngradeThreshold { get; set; } = 4000;

    /// <summary>
    /// Number of relevant messages to retrieve (used by VectorMemory).
    /// </summary>
    public int RetrieveTopK { get; set; } = 5;

    /// <summary>
    /// Minimum relevance score (used by VectorMemory, 0–1).
    /// </summary>
    public float MinRelevanceScore { get; set; } = 0.5f;

    /// <summary>
    /// Model name for the token counter.
    /// </summary>
    public string ModelName { get; set; } = "gpt-4";

    /// <summary>
    /// Maximum context token count.
    /// </summary>
    public int MaxContextTokens { get; set; } = 8192;

    /// <inheritdoc />
    public void Validate()
    {
        if (MaxContextTokens <= 0)
        {
            throw new InvalidOperationException("MaxContextTokens must be greater than 0.");
        }

        if (WindowSize <= 0)
        {
            throw new InvalidOperationException("WindowSize must be greater than 0.");
        }

        if (MaxRecentMessages <= 0)
        {
            throw new InvalidOperationException("MaxRecentMessages must be greater than 0.");
        }

        if (SummaryThreshold <= 0)
        {
            throw new InvalidOperationException("SummaryThreshold must be greater than 0.");
        }

        if (DowngradeThreshold <= 0)
        {
            throw new InvalidOperationException("DowngradeThreshold must be greater than 0.");
        }

        if (RetrieveTopK <= 0)
        {
            throw new InvalidOperationException("RetrieveTopK must be greater than 0.");
        }

        if (MinRelevanceScore < 0 || MinRelevanceScore > 1)
        {
            throw new InvalidOperationException("MinRelevanceScore must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(ModelName))
        {
            throw new InvalidOperationException("ModelName is required.");
        }

        if (
            Type is MemoryType.Summary or MemoryType.Adaptive
            && SummaryThreshold <= MaxRecentMessages
        )
        {
            throw new InvalidOperationException(
                "SummaryThreshold must be greater than MaxRecentMessages."
            );
        }
    }
}

/// <summary>
/// Memory type enumeration.
/// </summary>
public enum MemoryType
{
    /// <summary>
    /// Buffer memory — stores all messages.
    /// </summary>
    Buffer,

    /// <summary>
    /// Window memory — retains only the last N messages.
    /// </summary>
    Window,

    /// <summary>
    /// Summary memory — automatically summarizes old messages.
    /// </summary>
    Summary,

    /// <summary>
    /// Adaptive memory — automatically downgrades from Buffer to Summary.
    /// </summary>
    Adaptive,

    /// <summary>
    /// Vector memory — uses vector retrieval to enhance context relevance (Retrieve strategy).
    /// </summary>
    Vector,
}
