using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// BufferMemory 单元测试
/// </summary>
public class BufferMemoryTests
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();
    private readonly BufferMemory _memory;

    public BufferMemoryTests()
    {
        _memory = new BufferMemory(_tokenCounter);
    }

    [Fact]
    public async Task AddMessageAsync_SingleMessage_AddsToMemory()
    {
        var message = new ConversationMessage { Role = "user", Content = "Hello" };

        await _memory.AddMessageAsync(message);

        _memory.MessageCount.Should().Be(1);
    }

    [Fact]
    public async Task AddMessageAsync_MultipleMessages_AddsAllToMemory()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi there!" }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "How are you?" }
        );

        _memory.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task AddMessageAsync_CalculatesTokenCount_WhenNotProvided()
    {
        var message = new ConversationMessage { Role = "user", Content = "Hello World" };

        await _memory.AddMessageAsync(message);
        var messages = await _memory.GetMessagesAsync();

        messages[0].TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddMessageAsync_PreservesTokenCount_WhenProvided()
    {
        var message = new ConversationMessage
        {
            Role = "user",
            Content = "Hello",
            TokenCount = 100,
        };

        await _memory.AddMessageAsync(message);
        var messages = await _memory.GetMessagesAsync();

        messages[0].TokenCount.Should().Be(100);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsAllMessages_InOrder()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "First" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Second" }
        );
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Third" });

        var messages = await _memory.GetMessagesAsync();

        messages.Should().HaveCount(3);
        messages[0].Content.Should().Be("First");
        messages[1].Content.Should().Be("Second");
        messages[2].Content.Should().Be("Third");
    }

    [Fact]
    public async Task GetContextAsync_ReturnsAsChatMessages()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        var context = await _memory.GetContextAsync();

        context.Should().HaveCount(2);
        context[0].Role.Should().Be("user");
        context[0].Content.Should().Be("Hello");
        context[1].Role.Should().Be("assistant");
        context[1].Content.Should().Be("Hi!");
    }

    [Fact]
    public async Task GetContextAsync_WithMaxTokens_TrimsOldMessages()
    {
        // Add messages with known token counts
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "First message",
                TokenCount = 50,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Second message",
                TokenCount = 50,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Third message",
                TokenCount = 50,
            }
        );

        // Only allow 100 tokens - should get last 2 messages
        var context = await _memory.GetContextAsync(maxTokens: 100);

        context.Should().HaveCount(2);
        context[0].Content.Should().Be("Second message");
        context[1].Content.Should().Be("Third message");
    }

    [Fact]
    public async Task ClearAsync_RemovesAllMessages()
    {
        await _memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await _memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        await _memory.ClearAsync();

        _memory.MessageCount.Should().Be(0);
        var messages = await _memory.GetMessagesAsync();
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectTotal()
    {
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Hello",
                TokenCount = 10,
            }
        );
        await _memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "assistant",
                Content = "Hi!",
                TokenCount = 5,
            }
        );

        var tokenCount = await _memory.GetTokenCountAsync();

        tokenCount.Should().Be(15);
    }

    [Fact]
    public void Constructor_NullTokenCounter_ThrowsException()
    {
        var action = () => new BufferMemory(null!);
        action.Should().Throw<ArgumentNullException>();
    }
}
