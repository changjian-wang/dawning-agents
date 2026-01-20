using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// WindowMemory 单元测试
/// </summary>
public class WindowMemoryTests
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();

    [Fact]
    public async Task AddMessageAsync_UnderWindowSize_KeepsAllMessages()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 5);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "One" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Two" }
        );
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Three" });

        memory.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task AddMessageAsync_ExceedsWindowSize_RemovesOldestMessages()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 3);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "One" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Two" }
        );
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Three" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Four" }
        );
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Five" });

        memory.MessageCount.Should().Be(3);

        var messages = await memory.GetMessagesAsync();
        messages[0].Content.Should().Be("Three");
        messages[1].Content.Should().Be("Four");
        messages[2].Content.Should().Be("Five");
    }

    [Fact]
    public async Task WindowSize_ReturnsConfiguredValue()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 15);
        memory.WindowSize.Should().Be(15);
    }

    [Fact]
    public async Task GetContextAsync_ReturnsAsChatMessages()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 10);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        var context = await memory.GetContextAsync();

        context.Should().HaveCount(2);
        context[0].Role.Should().Be("user");
        context[1].Role.Should().Be("assistant");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllMessages()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 10);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.ClearAsync();

        memory.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectTotal()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 10);

        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Hello",
                TokenCount = 10,
            }
        );
        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Hi!",
                TokenCount = 5,
            }
        );

        var tokenCount = await memory.GetTokenCountAsync();
        tokenCount.Should().Be(15);
    }

    [Fact]
    public async Task GetTokenCountAsync_AfterWindowTrim_ExcludesRemovedMessages()
    {
        var memory = new WindowMemory(_tokenCounter, windowSize: 2);

        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "One",
                TokenCount = 100,
            }
        );
        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Two",
                TokenCount = 10,
            }
        );
        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Three",
                TokenCount = 5,
            }
        );

        // "One" should be removed, only "Two" (10) and "Three" (5) remain
        var tokenCount = await memory.GetTokenCountAsync();
        tokenCount.Should().Be(15);
    }

    [Fact]
    public void Constructor_NullTokenCounter_ThrowsException()
    {
        var action = () => new WindowMemory(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_InvalidWindowSize_ThrowsException(int windowSize)
    {
        var action = () => new WindowMemory(_tokenCounter, windowSize);
        action.Should().Throw<ArgumentException>();
    }
}
