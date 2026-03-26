namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Represents a message in the conversation history.
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// Unique identifier of the message.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Role: "user", "assistant", or "system".
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Message creation time.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional metadata (e.g., tool calls, token count).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Estimated token count for this message.
    /// </summary>
    public int? TokenCount { get; init; }
}
