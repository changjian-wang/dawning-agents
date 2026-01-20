using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// SimpleTokenCounter 单元测试
/// </summary>
public class SimpleTokenCounterTests
{
    private readonly SimpleTokenCounter _counter = new();

    [Fact]
    public void CountTokens_EmptyString_ReturnsZero()
    {
        var result = _counter.CountTokens("");
        result.Should().Be(0);
    }

    [Fact]
    public void CountTokens_NullString_ReturnsZero()
    {
        var result = _counter.CountTokens((string)null!);
        result.Should().Be(0);
    }

    [Theory]
    [InlineData("Hello", 2)] // 5 chars / 4 = 1.25, ceil = 2
    [InlineData("Hello World", 3)] // 11 chars / 4 = 2.75, ceil = 3
    [InlineData("a", 1)] // min 1
    public void CountTokens_EnglishText_ReturnsExpectedTokens(string text, int expectedTokens)
    {
        var result = _counter.CountTokens(text);
        result.Should().Be(expectedTokens);
    }

    [Theory]
    [InlineData("你好", 2)] // 2 chars / 1.5 = 1.33, ceil = 2
    [InlineData("你好世界", 3)] // 4 chars / 1.5 = 2.67, ceil = 3
    public void CountTokens_ChineseText_ReturnsExpectedTokens(string text, int expectedTokens)
    {
        var result = _counter.CountTokens(text);
        result.Should().Be(expectedTokens);
    }

    [Fact]
    public void CountTokens_MixedText_ReturnsCorrectCount()
    {
        // "Hello你好" = 5 english + 2 chinese
        // 5/4 + 2/1.5 = 1.25 + 1.33 = 2.58, ceil = 3
        var result = _counter.CountTokens("Hello你好");
        result.Should().Be(3);
    }

    [Fact]
    public void CountTokens_Messages_IncludesOverhead()
    {
        var messages = new[]
        {
            new Abstractions.LLM.ChatMessage("user", "Hello"),
            new Abstractions.LLM.ChatMessage("assistant", "Hi there"),
        };

        var result = _counter.CountTokens(messages);

        // Message 1: 4 (overhead) + 2 (Hello) = 6
        // Message 2: 4 (overhead) + 3 (Hi there, 8 chars / 4 = 2, ceil = 2, wait 8/4=2) = 6
        // Actually "Hi there" is 8 chars, 8/4 = 2
        // Total: 6 + 6 + 3 (reply prep) = 15
        // Let me recalc: "Hello" = 5 chars, 5/4 = 1.25, ceil = 2
        // "Hi there" = 8 chars, 8/4 = 2
        // M1: 4 + 2 = 6
        // M2: 4 + 2 = 6
        // Total: 6 + 6 + 3 = 15
        result.Should().Be(15);
    }

    [Fact]
    public void ModelName_ReturnsConfiguredValue()
    {
        var counter = new SimpleTokenCounter("gpt-4o", 128000);
        counter.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public void MaxContextTokens_ReturnsConfiguredValue()
    {
        var counter = new SimpleTokenCounter("gpt-4o", 128000);
        counter.MaxContextTokens.Should().Be(128000);
    }
}
