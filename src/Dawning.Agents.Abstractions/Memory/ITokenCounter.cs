using Dawning.Agents.Abstractions.LLM;

namespace Dawning.Agents.Abstractions.Memory;

/// <summary>
/// Token counter interface.
/// </summary>
/// <remarks>
/// <para>Used to count the number of tokens in text, helping manage the LLM context window.</para>
/// <para>Implementation types include: SimpleTokenCounter (estimation), TiktokenCounter (precise).</para>
/// </remarks>
public interface ITokenCounter
{
    /// <summary>
    /// Counts the number of tokens in the given text.
    /// </summary>
    /// <param name="text">The text to count.</param>
    /// <returns>Token count.</returns>
    int CountTokens(string text);

    /// <summary>
    /// Counts the token count for a message list (including role overhead).
    /// </summary>
    /// <param name="messages">Message list.</param>
    /// <returns>Total token count.</returns>
    int CountTokens(IEnumerable<ChatMessage> messages);

    /// <summary>
    /// Gets the model name corresponding to this counter.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Gets the maximum context window size.
    /// </summary>
    int MaxContextTokens { get; }
}
