using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;

namespace Dawning.Agents.Core.Memory;

/// <summary>
/// 使用基于字符估算的简单 Token 计数器
/// </summary>
/// <remarks>
/// <para>用于 tiktoken 不可用的场景</para>
/// <para>英文约 4 字符/token，中文约 1.5 字符/token</para>
/// </remarks>
public class SimpleTokenCounter : ITokenCounter
{
    private const double _englishCharsPerToken = 4.0;
    private const double _chineseCharsPerToken = 1.5;
    private const int _tokensPerMessage = 4;

    /// <summary>
    /// 获取此计数器关联的模型名称
    /// </summary>
    public string ModelName { get; }

    /// <summary>
    /// 获取模型的最大上下文 token 数
    /// </summary>
    public int MaxContextTokens { get; }

    /// <summary>
    /// 初始化简单 Token 计数器
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <param name="maxContextTokens">最大上下文 token 数</param>
    public SimpleTokenCounter(string modelName = "gpt-4", int maxContextTokens = 8192)
    {
        ModelName = modelName;
        MaxContextTokens = maxContextTokens;
    }

    /// <summary>
    /// 估算文本的 token 数量（英文约 4 字符/token，中文约 1.5 字符/token）
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
    /// 估算消息列表的 token 数量（包括每条消息的开销和回复预备）
    /// </summary>
    public int CountTokens(IEnumerable<ChatMessage> messages)
    {
        var total = 0;

        foreach (var message in messages)
        {
            total += _tokensPerMessage;
            total += CountTokens(message.Content);
        }

        // 回复预备 token
        return total + 3;
    }

    /// <summary>
    /// 判断是否为 CJK（中日韩）字符
    /// </summary>
    private static bool IsCjkChar(char c)
    {
        // CJK 统一汉字范围
        return c >= 0x4E00 && c <= 0x9FFF
            // CJK 扩展 A
            || c >= 0x3400 && c <= 0x4DBF
            // 日文假名
            || c >= 0x3040 && c <= 0x30FF
            // 韩文
            || c >= 0xAC00 && c <= 0xD7AF;
    }
}
