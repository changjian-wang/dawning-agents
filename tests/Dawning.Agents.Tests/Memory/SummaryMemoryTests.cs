using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// SummaryMemory 单元测试
/// </summary>
public class SummaryMemoryTests
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();
    private readonly Mock<ILLMProvider> _mockLlm = new();

    [Fact]
    public async Task AddMessageAsync_UnderThreshold_DoesNotSummarize()
    {
        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 6,
            summaryThreshold: 10
        );

        // Add 5 messages (under threshold of 10)
        for (int i = 0; i < 5; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Message {i}" }
            );
        }

        memory.Summary.Should().BeEmpty();
        _mockLlm.Verify(
            x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AddMessageAsync_ExceedsThreshold_TriggersSummarization()
    {
        _mockLlm
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "Summary of conversation" });

        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 3,
            summaryThreshold: 5
        );

        // Add 5 messages (hits threshold)
        for (int i = 0; i < 5; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Message {i}" }
            );
        }

        memory.Summary.Should().NotBeEmpty();
        _mockLlm.Verify(
            x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetMessagesAsync_WithSummary_IncludesSummaryAsSystemMessage()
    {
        _mockLlm
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "This is a summary" });

        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Add 4 messages to trigger summarization
        for (int i = 0; i < 4; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Message {i}" }
            );
        }

        var messages = await memory.GetMessagesAsync();

        // Should have: 1 summary system message + 2 recent messages
        messages.Should().HaveCount(3);
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Contain("This is a summary");
    }

    [Fact]
    public async Task GetContextAsync_ReturnsAsChatMessages()
    {
        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 6,
            summaryThreshold: 10
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });

        var context = await memory.GetContextAsync();

        context.Should().HaveCount(1);
        context[0].Role.Should().Be("user");
        context[0].Content.Should().Be("Hello");
    }

    [Fact]
    public async Task ClearAsync_ClearsSummaryAndMessages()
    {
        _mockLlm
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "Summary" });

        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Trigger summarization
        for (int i = 0; i < 4; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Message {i}" }
            );
        }

        memory.Summary.Should().NotBeEmpty();

        await memory.ClearAsync();

        memory.Summary.Should().BeEmpty();
        memory.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTokenCountAsync_IncludesSummaryTokens()
    {
        _mockLlm
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ChatCompletionResponse { Content = "Short summary" });

        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Trigger summarization
        for (int i = 0; i < 4; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = "user",
                    Content = $"Message {i}",
                    TokenCount = 10,
                }
            );
        }

        var tokenCount = await memory.GetTokenCountAsync();

        // Should include summary tokens + recent message tokens
        tokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Constructor_NullLlm_ThrowsException()
    {
        var action = () => new SummaryMemory(null!, _tokenCounter);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTokenCounter_ThrowsException()
    {
        var action = () => new SummaryMemory(_mockLlm.Object, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidMaxRecentMessages_ThrowsException(int maxRecentMessages)
    {
        var action = () =>
            new SummaryMemory(_mockLlm.Object, _tokenCounter, maxRecentMessages: maxRecentMessages);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_SummaryThresholdLessThanMaxRecent_ThrowsException()
    {
        // summaryThreshold (5) must be > maxRecentMessages (10)
        var action = () =>
            new SummaryMemory(
                _mockLlm.Object,
                _tokenCounter,
                maxRecentMessages: 10,
                summaryThreshold: 5
            );
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task MaxRecentMessages_ReturnsConfiguredValue()
    {
        var memory = new SummaryMemory(
            _mockLlm.Object,
            _tokenCounter,
            maxRecentMessages: 8,
            summaryThreshold: 15
        );

        memory.MaxRecentMessages.Should().Be(8);
    }
}
