using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// SimpleEmbeddingProvider 单元测试
/// </summary>
public class SimpleEmbeddingProviderTests
{
    private readonly SimpleEmbeddingProvider _provider;

    public SimpleEmbeddingProviderTests()
    {
        _provider = new SimpleEmbeddingProvider(dimensions: 128);
    }

    #region Constructor Tests

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(384)]
    [InlineData(512)]
    public void Constructor_WithValidDimensions_ShouldSetDimensions(int dimensions)
    {
        // Act
        var provider = new SimpleEmbeddingProvider(dimensions);

        // Assert
        provider.Dimensions.Should().Be(dimensions);
    }

    [Fact]
    public void Constructor_Default_ShouldUse384Dimensions()
    {
        // Act
        var provider = new SimpleEmbeddingProvider();

        // Assert
        provider.Dimensions.Should().Be(384);
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_ShouldReturnExpectedValue()
    {
        // Assert
        _provider.Name.Should().Be("SimpleEmbedding");
    }

    #endregion

    #region EmbedAsync Tests

    [Fact]
    public async Task EmbedAsync_ShouldReturnCorrectDimensions()
    {
        // Act
        var embedding = await _provider.EmbedAsync("Hello world");

        // Assert
        embedding.Should().HaveCount(128);
    }

    [Fact]
    public async Task EmbedAsync_SameText_ShouldReturnSameEmbedding()
    {
        // Act
        var embedding1 = await _provider.EmbedAsync("Hello world");
        var embedding2 = await _provider.EmbedAsync("Hello world");

        // Assert
        embedding1.Should().BeEquivalentTo(embedding2);
    }

    [Fact]
    public async Task EmbedAsync_DifferentText_ShouldReturnDifferentEmbedding()
    {
        // Act
        var embedding1 = await _provider.EmbedAsync("Hello world");
        var embedding2 = await _provider.EmbedAsync("Goodbye world");

        // Assert
        embedding1.Should().NotBeEquivalentTo(embedding2);
    }

    [Fact]
    public async Task EmbedAsync_ShouldReturnNormalizedVector()
    {
        // Act
        var embedding = await _provider.EmbedAsync("Test text");

        // Assert - magnitude should be close to 1.0
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        magnitude.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task EmbedAsync_EmptyText_ShouldReturnZeroVector()
    {
        // Act
        var embedding = await _provider.EmbedAsync("");

        // Assert
        embedding.Should().AllBeEquivalentTo(0f);
    }

    [Fact]
    public async Task EmbedAsync_SimilarText_ShouldHaveHighCosineSimilarity()
    {
        // Arrange
        var text1 = "The quick brown fox jumps over the lazy dog";
        var text2 = "A quick brown fox jumped over a lazy dog";

        // Act
        var embedding1 = await _provider.EmbedAsync(text1);
        var embedding2 = await _provider.EmbedAsync(text2);

        // Assert
        var similarity = CosineSimilarity(embedding1, embedding2);
        similarity.Should().BeGreaterThan(0.5); // Similar texts should have some similarity
    }

    [Fact]
    public async Task EmbedAsync_DifferentLanguages_ShouldReturnDifferentEmbeddings()
    {
        // Arrange
        var english = "Hello world";
        var chinese = "你好世界";

        // Act
        var embedding1 = await _provider.EmbedAsync(english);
        var embedding2 = await _provider.EmbedAsync(chinese);

        // Assert
        embedding1.Should().NotBeEquivalentTo(embedding2);
    }

    #endregion

    #region EmbedBatchAsync Tests

    [Fact]
    public async Task EmbedBatchAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var texts = new[] { "Text 1", "Text 2", "Text 3" };

        // Act
        var embeddings = await _provider.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().HaveCount(3);
    }

    [Fact]
    public async Task EmbedBatchAsync_EachEmbedding_ShouldHaveCorrectDimensions()
    {
        // Arrange
        var texts = new[] { "Text 1", "Text 2" };

        // Act
        var embeddings = await _provider.EmbedBatchAsync(texts);

        // Assert
        embeddings.Should().AllSatisfy(e => e.Should().HaveCount(128));
    }

    [Fact]
    public async Task EmbedBatchAsync_ShouldMatchIndividualEmbeddings()
    {
        // Arrange
        var texts = new[] { "Hello", "World" };

        // Act
        var batchEmbeddings = await _provider.EmbedBatchAsync(texts);
        var individual1 = await _provider.EmbedAsync("Hello");
        var individual2 = await _provider.EmbedAsync("World");

        // Assert
        batchEmbeddings[0].Should().BeEquivalentTo(individual1);
        batchEmbeddings[1].Should().BeEquivalentTo(individual2);
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyList_ShouldReturnEmpty()
    {
        // Act
        var embeddings = await _provider.EmbedBatchAsync([]);

        // Assert
        embeddings.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    #endregion
}
