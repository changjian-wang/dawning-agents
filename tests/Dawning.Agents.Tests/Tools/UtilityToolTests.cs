using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// UtilityTool 单元测试
/// </summary>
public class UtilityToolTests
{
    private readonly UtilityTool _tool = new();

    #region GenerateGuid Tests

    [Fact]
    public void GenerateGuid_DefaultFormat_ReturnsGuidWithHyphens()
    {
        var result = _tool.GenerateGuid();

        result.Should().MatchRegex(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"
        );
    }

    [Theory]
    [InlineData("N", @"^[0-9a-f]{32}$")]
    [InlineData("D", @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    [InlineData("B", @"^\{[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\}$")]
    [InlineData("P", @"^\([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\)$")]
    public void GenerateGuid_VariousFormats_ReturnsCorrectFormat(string format, string pattern)
    {
        var result = _tool.GenerateGuid(format);
        result.Should().MatchRegex(pattern);
    }

    [Fact]
    public void GenerateGuid_UnknownFormat_ReturnsDefaultFormat()
    {
        var result = _tool.GenerateGuid("X");
        result.Should().MatchRegex(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"
        );
    }

    #endregion

    #region GenerateRandomNumber Tests

    [Fact]
    public void GenerateRandomNumber_DefaultRange_ReturnsNumberInRange()
    {
        var result = _tool.GenerateRandomNumber();

        result.Should().Contain("随机数");
        result.Should().Contain("范围: 0-100");
    }

    [Fact]
    public void GenerateRandomNumber_CustomRange_ReturnsNumberInRange()
    {
        for (int i = 0; i < 10; i++)
        {
            var result = _tool.GenerateRandomNumber(10, 20);

            result.Should().Contain("范围: 10-20");
            // 提取数字并验证
            var match = System.Text.RegularExpressions.Regex.Match(result, @"随机数: (\d+)");
            match.Success.Should().BeTrue();
            var number = int.Parse(match.Groups[1].Value);
            number.Should().BeInRange(10, 20);
        }
    }

    [Fact]
    public void GenerateRandomNumber_MinGreaterThanMax_ReturnsError()
    {
        var result = _tool.GenerateRandomNumber(100, 10);
        result.Should().Contain("错误");
        result.Should().Contain("不能大于");
    }

    #endregion

    #region GenerateRandomString Tests

    [Fact]
    public void GenerateRandomString_DefaultLength_Returns16Chars()
    {
        var result = _tool.GenerateRandomString();
        result.Should().HaveLength(16);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(64)]
    public void GenerateRandomString_CustomLength_ReturnsCorrectLength(int length)
    {
        var result = _tool.GenerateRandomString(length);
        result.Should().HaveLength(length);
    }

    [Fact]
    public void GenerateRandomString_AlphaCharset_ReturnsOnlyLetters()
    {
        var result = _tool.GenerateRandomString(100, "alpha");
        result.Should().MatchRegex(@"^[a-zA-Z]+$");
    }

    [Fact]
    public void GenerateRandomString_NumericCharset_ReturnsOnlyDigits()
    {
        var result = _tool.GenerateRandomString(100, "numeric");
        result.Should().MatchRegex(@"^[0-9]+$");
    }

    [Fact]
    public void GenerateRandomString_HexCharset_ReturnsOnlyHex()
    {
        var result = _tool.GenerateRandomString(100, "hex");
        result.Should().MatchRegex(@"^[0-9A-F]+$");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void GenerateRandomString_InvalidLength_ReturnsError(int length)
    {
        var result = _tool.GenerateRandomString(length);
        result.Should().Contain("错误");
    }

    #endregion

    #region HashString Tests

    [Theory]
    [InlineData("MD5", 32)]
    [InlineData("SHA1", 40)]
    [InlineData("SHA256", 64)]
    [InlineData("SHA384", 96)]
    [InlineData("SHA512", 128)]
    public void HashString_VariousAlgorithms_ReturnsCorrectLength(string algorithm, int hexLength)
    {
        var result = _tool.HashString("test", algorithm);

        result.Should().Contain($"{algorithm}:");
        // 提取哈希值
        var hash = result.Split(": ")[1];
        hash.Should().HaveLength(hexLength);
    }

    [Fact]
    public void HashString_SameInput_ReturnsSameHash()
    {
        var hash1 = _tool.HashString("test", "SHA256");
        var hash2 = _tool.HashString("test", "SHA256");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashString_DifferentInput_ReturnsDifferentHash()
    {
        var hash1 = _tool.HashString("test1", "SHA256");
        var hash2 = _tool.HashString("test2", "SHA256");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashString_EmptyInput_ReturnsError()
    {
        var result = _tool.HashString("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void HashString_UnsupportedAlgorithm_ReturnsError()
    {
        var result = _tool.HashString("test", "UNSUPPORTED");
        result.Should().Contain("不支持的算法");
    }

    #endregion

    #region Base64 Tests

    [Fact]
    public void Base64Encode_ValidInput_ReturnsEncoded()
    {
        var result = _tool.Base64Encode("Hello, World!");
        result.Should().Be("SGVsbG8sIFdvcmxkIQ==");
    }

    [Fact]
    public void Base64Encode_EmptyInput_ReturnsError()
    {
        var result = _tool.Base64Encode("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void Base64Decode_ValidInput_ReturnsDecoded()
    {
        var result = _tool.Base64Decode("SGVsbG8sIFdvcmxkIQ==");
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void Base64Decode_EmptyInput_ReturnsError()
    {
        var result = _tool.Base64Decode("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void Base64Decode_InvalidInput_ReturnsError()
    {
        var result = _tool.Base64Decode("!!!invalid!!!");
        result.Should().Contain("错误");
    }

    [Fact]
    public void Base64_RoundTrip_ReturnsOriginal()
    {
        var original = "测试中文和特殊字符 !@#$%";
        var encoded = _tool.Base64Encode(original);
        var decoded = _tool.Base64Decode(encoded);

        decoded.Should().Be(original);
    }

    #endregion

    #region UrlEncode/Decode Tests

    [Fact]
    public void UrlEncode_ValidInput_ReturnsEncoded()
    {
        var result = _tool.UrlEncode("hello world");
        result.Should().Be("hello%20world");
    }

    [Fact]
    public void UrlEncode_SpecialChars_ReturnsEncoded()
    {
        var result = _tool.UrlEncode("a=1&b=2");
        result.Should().Contain("%");
    }

    [Fact]
    public void UrlEncode_EmptyInput_ReturnsError()
    {
        var result = _tool.UrlEncode("");
        result.Should().Contain("错误");
    }

    [Fact]
    public void UrlDecode_ValidInput_ReturnsDecoded()
    {
        var result = _tool.UrlDecode("hello%20world");
        result.Should().Be("hello world");
    }

    [Fact]
    public void UrlDecode_EmptyInput_ReturnsError()
    {
        var result = _tool.UrlDecode("");
        result.Should().Contain("错误");
    }

    #endregion

    #region RegexMatch Tests

    [Fact]
    public void RegexMatch_ValidPattern_ReturnsMatches()
    {
        var result = _tool.RegexMatch("hello world hello", "hello");
        result.Should().Contain("找到 2 个匹配");
    }

    [Fact]
    public void RegexMatch_NoMatch_ReturnsNoMatch()
    {
        var result = _tool.RegexMatch("hello world", "xyz");
        result.Should().Contain("没有找到匹配项");
    }

    [Fact]
    public void RegexMatch_EmptyInput_ReturnsError()
    {
        var result = _tool.RegexMatch("", @"\d+");
        result.Should().Contain("错误");
    }

    [Fact]
    public void RegexMatch_EmptyPattern_ReturnsError()
    {
        var result = _tool.RegexMatch("test", "");
        result.Should().Contain("错误");
    }

    [Fact]
    public void RegexMatch_InvalidPattern_ReturnsError()
    {
        var result = _tool.RegexMatch("test", "[invalid");
        result.Should().Contain("正则表达式错误");
    }

    #endregion

    #region StringStats Tests

    [Fact]
    public void StringStats_ValidInput_ReturnsStats()
    {
        var result = _tool.StringStats("Hello World\nLine 2");

        result.Should().Contain("总字符数");
        result.Should().Contain("单词数");
        result.Should().Contain("行数: 2");
    }

    [Fact]
    public void StringStats_EmptyInput_ReturnsError()
    {
        var result = _tool.StringStats("");
        result.Should().Contain("错误");
    }

    #endregion
}
