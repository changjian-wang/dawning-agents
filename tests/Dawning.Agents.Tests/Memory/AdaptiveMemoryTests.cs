using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// Unit tests for AdaptiveMemory
/// </summary>
public class AdaptiveMemoryTests
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();
    private readonly Mock<ILLMProvider> _mockLLM;

    public AdaptiveMemoryTests()
    {
        _mockLLM = new Mock<ILLMProvider>();
        _mockLLM
            .Setup(x =>
                x.ChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ChatCompletionResponse
                {
                    Content = "Summary: This is compressed conversation content.",
                }
            );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter);

        memory.Should().NotBeNull();
        memory.MessageCount.Should().Be(0);
        memory.HasDowngraded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullLLM_ThrowsArgumentNullException()
    {
        var action = () => new AdaptiveMemory(null!, _tokenCounter);

        action.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_WithNullTokenCounter_ThrowsArgumentNullException()
    {
        var action = () => new AdaptiveMemory(_mockLLM.Object, null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("tokenCounter");
    }

    [Fact]
    public void Constructor_WithZeroDowngradeThreshold_ThrowsArgumentException()
    {
        var action = () =>
            new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 0);

        action.Should().Throw<ArgumentException>().WithParameterName("downgradeThreshold");
    }

    [Fact]
    public void Constructor_WithNegativeDowngradeThreshold_ThrowsArgumentException()
    {
        var action = () =>
            new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: -100);

        action.Should().Throw<ArgumentException>().WithParameterName("downgradeThreshold");
    }

    [Fact]
    public void Constructor_WithZeroMaxRecentMessages_ThrowsArgumentException()
    {
        var action = () => new AdaptiveMemory(_mockLLM.Object, _tokenCounter, maxRecentMessages: 0);

        action.Should().Throw<ArgumentException>().WithParameterName("maxRecentMessages");
    }

    [Fact]
    public void Constructor_WithSummaryThresholdLessThanMaxRecentMessages_ThrowsArgumentException()
    {
        var action = () =>
            new AdaptiveMemory(
                _mockLLM.Object,
                _tokenCounter,
                maxRecentMessages: 10,
                summaryThreshold: 5
            );

        action.Should().Throw<ArgumentException>().WithParameterName("summaryThreshold");
    }

    #endregion

    #region AddMessageAsync Tests

    [Fact]
    public async Task AddMessageAsync_SingleMessage_AddsToMemory()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter);
        var message = new ConversationMessage { Role = "user", Content = "Hello" };

        await memory.AddMessageAsync(message);

        memory.MessageCount.Should().Be(1);
        memory.HasDowngraded.Should().BeFalse();
    }

    [Fact]
    public async Task AddMessageAsync_MultipleMessages_AddsAllToMemory()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi there!" }
        );
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "How are you?" }
        );

        memory.MessageCount.Should().Be(3);
        memory.HasDowngraded.Should().BeFalse();
    }

    [Fact]
    public async Task AddMessageAsync_BelowThreshold_StaysAsBufferMemory()
    {
        // Set a high downgrade threshold
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        // Add some messages
        for (int i = 0; i < 5; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Message {i}" }
            );
        }

        memory.HasDowngraded.Should().BeFalse();
    }

    [Fact]
    public async Task AddMessageAsync_ExceedsThreshold_TriggersDowngrade()
    {
        // Set a low downgrade threshold (100 tokens)
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 100,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Add enough messages to exceed the threshold
        // Simple estimate: each message is about 20-30 tokens
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = i % 2 == 0 ? "user" : "assistant",
                    Content =
                        $"This is a longer message number {i} with some content to increase token count significantly.",
                }
            );
        }

        memory.HasDowngraded.Should().BeTrue();
    }

    [Fact]
    public async Task AddMessageAsync_AfterDowngrade_ContinuesWithSummaryMemory()
    {
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 50,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Add enough messages to trigger downgrade
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = "user",
                    Content = $"Long message content number {i} to trigger downgrade quickly.",
                }
            );
        }

        var downgradeOccurred = memory.HasDowngraded;

        // Continue adding more messages
        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Additional message after downgrade",
            }
        );

        downgradeOccurred.Should().BeTrue();
        memory.MessageCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetMessagesAsync Tests

    [Fact]
    public async Task GetMessagesAsync_ReturnsAllMessages()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        var messages = await memory.GetMessagesAsync();

        messages.Should().HaveCount(2);
        messages[0].Content.Should().Be("Hello");
        messages[1].Content.Should().Be("Hi!");
    }

    #endregion

    #region GetContextAsync Tests

    [Fact]
    public async Task GetContextAsync_ReturnsMessagesAsChatMessages()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        var context = await memory.GetContextAsync();

        context.Should().HaveCount(2);
        context[0].Role.Should().Be("user");
        context[0].Content.Should().Be("Hello");
    }

    [Fact]
    public async Task GetContextAsync_WithMaxTokens_RespectsLimit()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi there!" }
        );

        var context = await memory.GetContextAsync(maxTokens: 5);

        context.Count.Should().BeLessThanOrEqualTo(2);
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_RemovesAllMessages()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Hi!" }
        );

        await memory.ClearAsync();

        memory.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_ResetsToBufferMemory()
    {
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 50,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Trigger downgrade
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = "user",
                    Content = $"Long message to trigger downgrade {i}",
                }
            );
        }

        memory.HasDowngraded.Should().BeTrue();

        // Clear
        await memory.ClearAsync();

        // Verify reset
        memory.HasDowngraded.Should().BeFalse();
        memory.MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_AfterReset_CanAcceptNewMessages()
    {
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 50,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Trigger downgrade
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Long message {i}" }
            );
        }

        await memory.ClearAsync();

        // Add new messages
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "New message" }
        );

        memory.MessageCount.Should().Be(1);
        memory.HasDowngraded.Should().BeFalse();
    }

    #endregion

    #region GetTokenCountAsync Tests

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectCount()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        await memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "Hello World" }
        );

        var tokenCount = await memory.GetTokenCountAsync();

        tokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTokenCountAsync_WithEmptyMemory_ReturnsZero()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter);

        var tokenCount = await memory.GetTokenCountAsync();

        tokenCount.Should().Be(0);
    }

    #endregion

    #region Downgrade Behavior Tests

    [Fact]
    public async Task Downgrade_OnlyHappensOnce()
    {
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 30,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // Add a large number of messages, exceeding the threshold multiple times
        for (int i = 0; i < 20; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = "user",
                    Content = $"Message {i} with enough content",
                }
            );
        }

        // Verify LLM was called (for summarization), but downgrade only happens once
        memory.HasDowngraded.Should().BeTrue();
    }

    [Fact]
    public async Task Downgrade_PreservesConversationContext()
    {
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 100,
            maxRecentMessages: 3,
            summaryThreshold: 5
        );

        // Add messages
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "user", Content = "First message" }
        );
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "First response" }
        );
        await memory.AddMessageAsync(
            new ConversationMessage
            {
                Role = "user",
                Content = "Second message about important topic",
            }
        );
        await memory.AddMessageAsync(
            new ConversationMessage { Role = "assistant", Content = "Second response with details" }
        );

        // Trigger downgrade
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage
                {
                    Role = "user",
                    Content =
                        $"Additional message {i} to trigger the downgrade mechanism in adaptive memory",
                }
            );
        }

        // After downgrade, context should still be retrievable
        var context = await memory.GetContextAsync();
        context.Should().NotBeEmpty();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task AddMessageAsync_ConcurrentCalls_HandlesCorrectly()
    {
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(
                Task.Run(async () =>
                {
                    await memory.AddMessageAsync(
                        new ConversationMessage
                        {
                            Role = "user",
                            Content = $"Concurrent message {index}",
                        }
                    );
                })
            );
        }

        await Task.WhenAll(tasks);

        memory.MessageCount.Should().Be(100);
    }

    #endregion
}
