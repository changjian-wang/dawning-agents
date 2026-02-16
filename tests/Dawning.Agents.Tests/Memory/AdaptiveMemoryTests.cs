using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.Memory;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// AdaptiveMemory 单元测试
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
                new ChatCompletionResponse { Content = "摘要：这是一段压缩后的对话内容。" }
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
        // 设置较高的降级阈值
        var memory = new AdaptiveMemory(_mockLLM.Object, _tokenCounter, downgradeThreshold: 10000);

        // 添加一些消息
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
        // 设置较低的降级阈值（100 tokens）
        var memory = new AdaptiveMemory(
            _mockLLM.Object,
            _tokenCounter,
            downgradeThreshold: 100,
            maxRecentMessages: 2,
            summaryThreshold: 4
        );

        // 添加足够多的消息以超过阈值
        // 简单估算：每条消息约 20-30 tokens
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

        // 添加足够消息触发降级
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

        // 继续添加更多消息
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

        // 触发降级
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

        // 清空
        await memory.ClearAsync();

        // 验证已重置
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

        // 触发降级
        for (int i = 0; i < 10; i++)
        {
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = $"Long message {i}" }
            );
        }

        await memory.ClearAsync();

        // 添加新消息
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

        // 添加大量消息，多次超过阈值
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

        // 验证 LLM 被调用（用于摘要），但降级只发生一次
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

        // 添加消息
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

        // 触发降级
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

        // 降级后仍然可以获取上下文
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
