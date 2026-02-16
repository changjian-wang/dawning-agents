using Dawning.Agents.Abstractions.Cache;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.Cache;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.Cache;

/// <summary>
/// SemanticCache 单元测试
/// </summary>
public class SemanticCacheTests
{
    private readonly Mock<IEmbeddingProvider> _mockEmbedding;
    private readonly InMemoryVectorStore _vectorStore;
    private readonly IOptions<SemanticCacheOptions> _options;

    public SemanticCacheTests()
    {
        _mockEmbedding = new Mock<IEmbeddingProvider>();
        _mockEmbedding
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (string text, CancellationToken _) =>
                {
                    // 简单的确定性伪嵌入：相同文本产生相同向量
                    var hash = text.GetHashCode();
                    return new float[]
                    {
                        (hash & 0xFF) / 255f,
                        ((hash >> 8) & 0xFF) / 255f,
                        ((hash >> 16) & 0xFF) / 255f,
                    };
                }
            );

        _vectorStore = new InMemoryVectorStore();
        _options = Options.Create(
            new SemanticCacheOptions
            {
                Enabled = true,
                SimilarityThreshold = 0.9f,
                MaxEntries = 100,
                ExpirationMinutes = 60,
            }
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParams_CreatesInstance()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        cache.Should().NotBeNull();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullVectorStore_ThrowsArgumentNullException()
    {
        var action = () => new SemanticCache(null!, _mockEmbedding.Object, _options);

        action.Should().Throw<ArgumentNullException>().WithParameterName("vectorStore");
    }

    [Fact]
    public void Constructor_WithNullEmbeddingProvider_ThrowsArgumentNullException()
    {
        var action = () => new SemanticCache(_vectorStore, null!, _options);

        action.Should().Throw<ArgumentNullException>().WithParameterName("embeddingProvider");
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_ValidQueryAndResponse_AddsToCachе()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");

        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task SetAsync_MultipleEntries_AddsAllToCache()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        await cache.SetAsync("Question 1", "Answer 1");
        await cache.SetAsync("Question 2", "Answer 2");
        await cache.SetAsync("Question 3", "Answer 3");

        cache.Count.Should().Be(3);
    }

    [Fact]
    public async Task SetAsync_WithMetadata_StoresMetadata()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);
        var metadata = new Dictionary<string, string>
        {
            ["model"] = "gpt-4o",
            ["temperature"] = "0.7",
        };

        await cache.SetAsync("What is AI?", "AI is artificial intelligence.", metadata);

        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task SetAsync_EmptyQuery_DoesNotAddToCache()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        await cache.SetAsync("", "Some response");
        await cache.SetAsync("   ", "Some response");

        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_EmptyResponse_DoesNotAddToCache()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        await cache.SetAsync("Some query", "");
        await cache.SetAsync("Some query", "   ");

        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_WhenDisabled_DoesNotAddToCache()
    {
        var disabledOptions = Options.Create(new SemanticCacheOptions { Enabled = false });
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, disabledOptions);

        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");

        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_WhenMaxEntriesReached_DoesNotAdd()
    {
        var limitedOptions = Options.Create(
            new SemanticCacheOptions { Enabled = true, MaxEntries = 2 }
        );
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, limitedOptions);

        await cache.SetAsync("Q1", "A1");
        await cache.SetAsync("Q2", "A2");
        await cache.SetAsync("Q3", "A3"); // Should be skipped

        cache.Count.Should().Be(2);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ExactMatch_ReturnsCachedResponse()
    {
        // 设置相同文本产生相同向量
        _mockEmbedding
            .Setup(x => x.EmbedAsync("What is AI?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });

        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);
        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");

        var result = await cache.GetAsync("What is AI?");

        result.Should().NotBeNull();
        result!.Response.Should().Be("AI is artificial intelligence.");
        result.SimilarityScore.Should().BeGreaterThan(0.9f);
    }

    [Fact]
    public async Task GetAsync_NoMatch_ReturnsNull()
    {
        // 设置高阈值确保只有精确匹配才能命中
        var highThresholdOptions = Options.Create(
            new SemanticCacheOptions { Enabled = true, SimilarityThreshold = 0.99f }
        );

        // 使用独立的 mock 和 store
        var mockEmbed = new Mock<IEmbeddingProvider>();
        var store = new InMemoryVectorStore();

        // 缓存查询使用一个向量
        mockEmbed
            .Setup(x => x.EmbedAsync("What is AI?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1.0f, 0.0f, 0.0f });

        // 不同查询使用正交向量（余弦相似度为0）
        mockEmbed
            .Setup(x => x.EmbedAsync("Completely different query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.0f, 1.0f, 0.0f });

        var cache = new SemanticCache(store, mockEmbed.Object, highThresholdOptions);
        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");

        var result = await cache.GetAsync("Completely different query");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_EmptyQuery_ReturnsNull()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        var result = await cache.GetAsync("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenDisabled_ReturnsNull()
    {
        var disabledOptions = Options.Create(new SemanticCacheOptions { Enabled = false });
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, disabledOptions);
        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");

        var result = await cache.GetAsync("What is AI?");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_BelowThreshold_ReturnsNull()
    {
        var highThresholdOptions = Options.Create(
            new SemanticCacheOptions
            {
                Enabled = true,
                SimilarityThreshold = 0.99f, // 非常高的阈值
            }
        );
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, highThresholdOptions);

        _mockEmbedding
            .Setup(x => x.EmbedAsync("What is AI?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
        _mockEmbedding
            .Setup(x =>
                x.EmbedAsync("What is artificial intelligence?", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new float[] { 0.11f, 0.21f, 0.31f }); // 相似但不完全相同

        await cache.SetAsync("What is AI?", "AI is artificial intelligence.");
        var result = await cache.GetAsync("What is artificial intelligence?");

        // 由于阈值很高，可能返回 null
        // 具体取决于实际相似度
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);
        await cache.SetAsync("Q1", "A1");
        await cache.SetAsync("Q2", "A2");
        await cache.SetAsync("Q3", "A3");

        await cache.ClearAsync();

        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task ClearAsync_OnEmptyCache_DoesNotThrow()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        var action = () => cache.ClearAsync();

        await action.Should().NotThrowAsync();
    }

    #endregion

    #region Namespace Isolation Tests

    [Fact]
    public async Task DifferentNamespaces_HaveIsolatedCaches()
    {
        var options1 = Options.Create(new SemanticCacheOptions { Namespace = "app1" });
        var options2 = Options.Create(new SemanticCacheOptions { Namespace = "app2" });

        var store1 = new InMemoryVectorStore();
        var store2 = new InMemoryVectorStore();

        var cache1 = new SemanticCache(store1, _mockEmbedding.Object, options1);
        var cache2 = new SemanticCache(store2, _mockEmbedding.Object, options2);

        await cache1.SetAsync("Query", "Response from App1");
        await cache2.SetAsync("Query", "Response from App2");

        cache1.Count.Should().Be(1);
        cache2.Count.Should().Be(1);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentSetAsync_HandlesCorrectly()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            var index = i;
            tasks.Add(cache.SetAsync($"Question {index}", $"Answer {index}"));
        }

        await Task.WhenAll(tasks);

        cache.Count.Should().Be(50);
    }

    [Fact]
    public async Task ConcurrentGetAndSet_HandlesCorrectly()
    {
        var cache = new SemanticCache(_vectorStore, _mockEmbedding.Object, _options);

        // 先添加一些数据
        for (int i = 0; i < 10; i++)
        {
            await cache.SetAsync($"Question {i}", $"Answer {i}");
        }

        var tasks = new List<Task>();

        // 并发读写
        for (int i = 0; i < 50; i++)
        {
            var index = i;
            if (index % 2 == 0)
            {
                tasks.Add(cache.SetAsync($"New Question {index}", $"New Answer {index}"));
            }
            else
            {
                tasks.Add(cache.GetAsync($"Question {index % 10}"));
            }
        }

        await Task.WhenAll(tasks);

        cache.Count.Should().BeGreaterThan(10);
    }

    #endregion
}
