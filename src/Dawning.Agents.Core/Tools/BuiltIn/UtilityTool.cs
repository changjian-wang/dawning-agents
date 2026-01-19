using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 通用工具函数
/// </summary>
public class UtilityTool
{
    /// <summary>
    /// 生成 GUID
    /// </summary>
    [FunctionTool("生成全局唯一标识符 (GUID/UUID)", Category = "Utility")]
    public string GenerateGuid(
        [ToolParameter("格式: N(无连字符), D(带连字符), B(带大括号), P(带圆括号)")]
            string format = "D"
    )
    {
        var guid = Guid.NewGuid();
        return format.ToUpperInvariant() switch
        {
            "N" => guid.ToString("N"), // 32位无连字符
            "D" => guid.ToString("D"), // 带连字符
            "B" => guid.ToString("B"), // 带大括号
            "P" => guid.ToString("P"), // 带圆括号
            _ => guid.ToString("D"),
        };
    }

    /// <summary>
    /// 生成随机数
    /// </summary>
    [FunctionTool("生成指定范围内的随机整数", Category = "Utility")]
    public string GenerateRandomNumber(
        [ToolParameter("最小值（包含）")] int min = 0,
        [ToolParameter("最大值（包含）")] int max = 100
    )
    {
        if (min > max)
        {
            return $"错误: 最小值 ({min}) 不能大于最大值 ({max})";
        }

        var random = Random.Shared.Next(min, max + 1);
        return $"随机数: {random} (范围: {min}-{max})";
    }

    /// <summary>
    /// 生成随机字符串
    /// </summary>
    [FunctionTool("生成指定长度的随机字符串", Category = "Utility")]
    public string GenerateRandomString(
        [ToolParameter("字符串长度")] int length = 16,
        [ToolParameter(
            "字符集: alphanumeric(字母数字), alpha(仅字母), numeric(仅数字), hex(十六进制)"
        )]
            string charset = "alphanumeric"
    )
    {
        if (length <= 0 || length > 1000)
        {
            return "错误: 长度必须在 1-1000 之间";
        }

        var chars = charset.ToLowerInvariant() switch
        {
            "alpha" => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
            "numeric" => "0123456789",
            "hex" => "0123456789ABCDEF",
            _ => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789",
        };

        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            result.Append(chars[Random.Shared.Next(chars.Length)]);
        }

        return result.ToString();
    }

    /// <summary>
    /// 字符串哈希
    /// </summary>
    [FunctionTool("计算字符串的哈希值", Category = "Utility")]
    public string HashString(
        [ToolParameter("要计算哈希的字符串")] string input,
        [ToolParameter("哈希算法: MD5, SHA1, SHA256, SHA384, SHA512")] string algorithm = "SHA256"
    )
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入字符串不能为空";
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash;

        try
        {
            hash = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.HashData(bytes),
                "SHA1" => SHA1.HashData(bytes),
                "SHA256" => SHA256.HashData(bytes),
                "SHA384" => SHA384.HashData(bytes),
                "SHA512" => SHA512.HashData(bytes),
                _ => throw new ArgumentException($"不支持的算法: {algorithm}"),
            };
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }

        return $"{algorithm}: {Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    /// <summary>
    /// Base64 编码
    /// </summary>
    [FunctionTool("将字符串进行 Base64 编码", Category = "Utility")]
    public string Base64Encode([ToolParameter("要编码的字符串")] string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入字符串不能为空";
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Base64 解码
    /// </summary>
    [FunctionTool("将 Base64 字符串解码", Category = "Utility")]
    public string Base64Decode([ToolParameter("要解码的 Base64 字符串")] string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入字符串不能为空";
        }

        try
        {
            var bytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return "错误: 无效的 Base64 字符串";
        }
    }

    /// <summary>
    /// URL 编码
    /// </summary>
    [FunctionTool("对字符串进行 URL 编码", Category = "Utility")]
    public string UrlEncode([ToolParameter("要编码的字符串")] string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入字符串不能为空";
        }

        return Uri.EscapeDataString(input);
    }

    /// <summary>
    /// URL 解码
    /// </summary>
    [FunctionTool("对 URL 编码的字符串进行解码", Category = "Utility")]
    public string UrlDecode([ToolParameter("要解码的字符串")] string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入字符串不能为空";
        }

        return Uri.UnescapeDataString(input);
    }

    /// <summary>
    /// 正则表达式匹配
    /// </summary>
    [FunctionTool("使用正则表达式匹配字符串", Category = "Utility")]
    public string RegexMatch(
        [ToolParameter("要匹配的文本")] string input,
        [ToolParameter("正则表达式模式")] string pattern
    )
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入文本不能为空";
        }

        if (string.IsNullOrEmpty(pattern))
        {
            return "错误: 正则表达式不能为空";
        }

        try
        {
            var matches = Regex.Matches(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            if (matches.Count == 0)
            {
                return "没有找到匹配项";
            }

            var results = new StringBuilder();
            results.AppendLine($"找到 {matches.Count} 个匹配:");
            for (int i = 0; i < Math.Min(matches.Count, 10); i++)
            {
                results.AppendLine($"  [{i}]: \"{matches[i].Value}\" (位置: {matches[i].Index})");
            }
            if (matches.Count > 10)
            {
                results.AppendLine($"  ... 还有 {matches.Count - 10} 个匹配");
            }

            return results.ToString().TrimEnd();
        }
        catch (RegexParseException ex)
        {
            return $"正则表达式错误: {ex.Message}";
        }
        catch (RegexMatchTimeoutException)
        {
            return "错误: 正则表达式匹配超时";
        }
    }

    /// <summary>
    /// 字符串统计
    /// </summary>
    [FunctionTool("统计字符串的字符数、单词数、行数等信息", Category = "Utility")]
    public string StringStats([ToolParameter("要统计的文本")] string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "错误: 输入文本不能为空";
        }

        var charCount = input.Length;
        var charCountNoSpace = input.Count(c => !char.IsWhiteSpace(c));
        var wordCount = input
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var lineCount = input.Split('\n').Length;

        return $"""
            字符串统计:
            - 总字符数: {charCount}
            - 非空白字符: {charCountNoSpace}
            - 单词数: {wordCount}
            - 行数: {lineCount}
            """;
    }
}
