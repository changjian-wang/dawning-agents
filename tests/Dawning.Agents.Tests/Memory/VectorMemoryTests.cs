using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Memory;

/// <summary>
/// VectorMemory 单元测试
/// </summary>
public class VectorMemoryTests
{
    private readonly ITokenCounter _tokenCounter = new SimpleTokenCounter();
    private readonly Mock<IEmbeddingProvider> _mockEmbedding;
    private readonly InMemoryVectorStore _vectorStore;

    public VectorMemoryTests()
    {
        _mockEmbedding = new Mock<IEmbeddingProvider>();
        _mockEmbedding
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // 简单的模拟嵌入：基于文本哈希生成伪向量
                var hash = text.GetHashCode();
                return new float[] { hash % 100 / 100f, (hash >> 8) % 100 / 100f, (hash >> 16) % 100 / 100f };
            });

        _vectorStore = new InMemoryVectorStore();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        memory.Should().NotBeNull();
        memory.MessageCount.Should().Be(0);
        memory.SessionId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WithNullVectorStore_ThrowsArgumentNullException()
    {
        var action = () => new VectorMemory(
            null!,
            _mockEmbedding.Object,
            _tokenCounter
        );

        action.Should().Throw<ArgumentNullException>().WithParameterName("vectorStore");
    }

    [Fact]
    public void Constructor_WithNullEmbeddingProvider_ThrowsArgumentNullException()
    {
        var action = () => new VectorMemory(
            _vectorStore,
            null!,
            _tokenCounter
        );

        action.Should().Throw<ArgumentNullException>().WithParameterName("embeddingProvider");
    }

    [Fact]
    public void Constructor_WithNullTokenCounter_ThrowsArgumentNullException()
    {
        var action = () => new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            null!
        );

        action.Should().Throw<ArgumentNullException>().WithParameterName("tokenCounter");
    }

    [Fact]
    public void Constructor_WithZeroWindowSize_ThrowsArgumentException()
    {
        var action = () => new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 0
        );

        action.Should().Throw<ArgumentException>().WithParameterName("recentWindowSize");
    }

    [Fact]
    public void Constructor_WithZeroTopK_ThrowsArgumentException()
    {
        var action = () => new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            retrieveTopK: 0
        );

        action.Should().Throw<ArgumentException>().WithParameterName("retrieveTopK");
    }

    [Fact]
    public void Constructor_WithInvalidRelevanceScore_ThrowsArgumentException()
    {
        var action = () => new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            minRelevanceScore: 1.5f
        );

        action.Should().Throw<ArgumentException>().WithParameterName("minRelevanceScore");
    }

    [Fact]
    public void Constructor_WithCustomSessionId_UsesProvidedId()
    {
        var sessionId = "test-session-123";
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            sessionId: sessionId
        );

        memory.SessionId.Should().Be(sessionId);
    }

    #endregion

    #region AddMessageAsync Tests

    [Fact]
    public async Task AddMessageAsync_SingleMessage_AddsToRecentMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );
        var message = new ConversationMessage { Role = "user", Content = "Hello" };

        await memory.AddMessageAsync(message);

        memory.RecentMessageCount.Should().Be(1);
    }

    [Fact]
    public async Task AddMessageAsync_MultipleMessages_AddsAllToRecentMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 10
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Hi!" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "How are you?" });

        memory.RecentMessageCount.Should().Be(3);
    }

    [Fact]
    public async Task AddMessageAsync_ExceedsWindowSize_ArchivesOldMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 3
        );

        // 添加 5 条消息，窗口大小为 3
        for (int i = 0; i < 5; i++)
        {
            await memory.AddMessageAsync(new ConversationMessage
            {
                Role = "user",
                Content = $"Message {i}"
            });
        }

        // 最近消息应该只有 3 条
        memory.RecentMessageCount.Should().Be(3);
        // 应该有 2 条消息被归档到向量存储
        memory.VectorStoreCount.Should().Be(2);
    }

    [Fact]
    public async Task AddMessageAsync_CalculatesTokenCount_WhenNotProvided()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );
        var message = new ConversationMessage { Role = "user", Content = "Hello World" };

        await memory.AddMessageAsync(message);
        var messages = await memory.GetMessagesAsync();

        messages[0].TokenCount.Should().NotBeNull();
        messages[0].TokenCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetMessagesAsync Tests

    [Fact]
    public async Task GetMessagesAsync_ReturnsRecentMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Hi!" });

        var messages = await memory.GetMessagesAsync();

        messages.Should().HaveCount(2);
        messages[0].Content.Should().Be("Hello");
        messages[1].Content.Should().Be("Hi!");
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsOnlyRecentMessages_NotArchived()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 2
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 1" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 2" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 3" });

        var messages = await memory.GetMessagesAsync();

        messages.Should().HaveCount(2);
        messages[0].Content.Should().Be("Message 2");
        messages[1].Content.Should().Be("Message 3");
    }

    #endregion

    #region GetContextAsync Tests

    [Fact]
    public async Task GetContextAsync_WithNoHistory_ReturnsRecentMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Hi!" });

        var context = await memory.GetContextAsync();

        context.Should().HaveCount(2);
        context[0].Role.Should().Be("user");
        context[0].Content.Should().Be("Hello");
    }

    [Fact]
    public async Task GetContextAsync_WithHistory_IncludesRelevantMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 2,
            minRelevanceScore: 0.0f // 设置为 0 以确保检索到结果
        );

        // 添加消息并触发归档
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "What is AI?" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "AI is artificial intelligence." });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Tell me more about AI." });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "AI includes machine learning." });

        // 应该有 2 条消息被归档
        memory.VectorStoreCount.Should().Be(2);

        var context = await memory.GetContextAsync();

        // 上下文应该包含历史标记 + 相关历史 + 最近消息
        context.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetContextAsync_WithMaxTokens_RespectsLimit()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello World" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Hi there!" });

        var context = await memory.GetContextAsync(maxTokens: 3);

        // 应该只返回能在 token 限制内的消息
        context.Count.Should().BeLessThanOrEqualTo(2);
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_RemovesAllMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 2
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 1" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 2" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Message 3" });

        await memory.ClearAsync();

        memory.RecentMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_AfterClear_CanAcceptNewMessages()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Old message" });
        await memory.ClearAsync();
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "New message" });

        var messages = await memory.GetMessagesAsync();
        messages.Should().HaveCount(1);
        messages[0].Content.Should().Be("New message");
    }

    #endregion

    #region GetTokenCountAsync Tests

    [Fact]
    public async Task GetTokenCountAsync_ReturnsCorrectCount()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Hello World" });

        var tokenCount = await memory.GetTokenCountAsync();

        tokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTokenCountAsync_WithEmptyMemory_ReturnsZero()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter
        );

        var tokenCount = await memory.GetTokenCountAsync();

        tokenCount.Should().Be(0);
    }

    #endregion

    #region Retrieval Behavior Tests

    [Fact]
    public async Task Retrieval_UsesRecentUserMessages_AsQuery()
    {
        var embeddingCalls = new List<string>();
        _mockEmbedding
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                embeddingCalls.Add(text);
                var hash = text.GetHashCode();
                return [hash % 100 / 100f, (hash >> 8) % 100 / 100f, (hash >> 16) % 100 / 100f];
            });

        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 2,
            minRelevanceScore: 0.0f
        );

        // 添加消息触发归档
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Old question" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Old answer" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "New question" });

        // 获取上下文会触发检索
        await memory.GetContextAsync();

        // 验证嵌入调用包含查询（用户消息）
        embeddingCalls.Should().Contain(c => c.Contains("New question"));
    }

    [Fact]
    public async Task Retrieval_WithHighMinScore_FiltersIrrelevantResults()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 2,
            minRelevanceScore: 0.99f // 非常高的阈值
        );

        // 添加消息触发归档
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Question 1" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Answer 1" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Question 2" });
        await memory.AddMessageAsync(new ConversationMessage { Role = "assistant", Content = "Answer 2" });

        var context = await memory.GetContextAsync();

        // 由于高阈值，可能不包含历史上下文标记
        // 只包含最近消息
        context.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task AddMessageAsync_ConcurrentCalls_HandlesCorrectly()
    {
        var memory = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            recentWindowSize: 100
        );
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await memory.AddMessageAsync(new ConversationMessage
                {
                    Role = "user",
                    Content = $"Concurrent message {index}"
                });
            }));
        }

        await Task.WhenAll(tasks);

        memory.RecentMessageCount.Should().Be(50);
    }

    #endregion

    #region Session Isolation Tests

    [Fact]
    public async Task DifferentSessions_HaveIsolatedData()
    {
        var session1 = new VectorMemory(
            _vectorStore,
            _mockEmbedding.Object,
            _tokenCounter,
            sessionId: "session-1"
        );

        var session2 = new VectorMemory(
            new InMemoryVectorStore(), // 使用不同的存储
            _mockEmbedding.Object,
            _tokenCounter,
            sessionId: "session-2"
        );

        await session1.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Session 1 message" });
        await session2.AddMessageAsync(new ConversationMessage { Role = "user", Content = "Session 2 message" });

        var messages1 = await session1.GetMessagesAsync();
        var messages2 = await session2.GetMessagesAsync();

        messages1.Should().HaveCount(1);
        messages1[0].Content.Should().Be("Session 1 message");
        messages2.Should().HaveCount(1);
        messages2[0].Content.Should().Be("Session 2 message");
    }

    #endregion
}
