using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// A simple token counter that uses character-based estimation.
/// </summary>
/// <remarks>
/// <para>Used when tiktoken is not available.</para>
/// <para>Estimates approximately 4 characters per token for English and 1.5 characters per token for Chinese.</para>
/// </remarks>
public class SimpleTokenCounter : ITokenCounter
{
    private const double _englishCharsPerToken = 4.0;
    private const double _chineseCharsPerToken = 1.5;
    private const int _tokensPerMessage = 4;

    /// <summary>
    /// Gets the model name associated with this counter.
    /// </summary>
    public string ModelName { get; }

    /// <summary>
    /// Gets the maximum context token count for the model.
    /// </summary>
    public int MaxContextTokens { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleTokenCounter"/> class.
    /// </summary>
    /// <param name="modelName">The model name.</param>
    /// <param name="maxContextTokens">The maximum context token count.</param>
    public SimpleTokenCounter(string modelName = "gpt-4", int maxContextTokens = 8192)
    {
        ModelName = modelName;
        MaxContextTokens = maxContextTokens;
    }

    /// <summary>
    /// Estimates the token count for the given text (approximately 4 characters per token for English, 1.5 for Chinese).
    /// </summary>
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var englishChars = 0;
        var chineseChars = 0;

        foreach (var c in text)
        {
            if (IsCjkChar(c))
            {
                chineseChars++;
            }
            else
            {
                englishChars++;
            }
        }

        var tokens = (int)
            Math.Ceiling(
                englishChars / _englishCharsPerToken + chineseChars / _chineseCharsPerToken
            );

        return Math.Max(1, tokens);
    }

    /// <summary>
    /// Estimates the token count for a list of messages (including per-message overhead and reply priming).
    /// </summary>
    public int CountTokens(IEnumerable<ChatMessage> messages)
    {
        var total = 0;

        foreach (var message in messages)
        {
            total += _tokensPerMessage;
            total += CountTokens(message.Content);
        }

        // Reply priming tokens
        return total + 3;
    }

    /// <summary>
    /// Determines whether the character is a CJK (Chinese, Japanese, Korean) character.
    /// </summary>
    private static bool IsCjkChar(char c)
    {
        // CJK Unified Ideographs
        return c >= 0x4E00 && c <= 0x9FFF
            // CJK Unified Ideographs Extension A
            || c >= 0x3400 && c <= 0x4DBF
            // Japanese Kana
            || c >= 0x3040 && c <= 0x30FF
            // Korean Hangul Syllables
            || c >= 0xAC00 && c <= 0xD7AF;
    }
}
