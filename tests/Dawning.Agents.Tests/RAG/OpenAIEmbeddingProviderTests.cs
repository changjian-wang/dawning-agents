using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.OpenAI;
using FluentAssertions;

namespace Dawning.Agents.Tests.RAG;

public class OpenAIEmbeddingProviderTests
{
    [Fact]
    public void Constructor_WithValidApiKey_CreatesProvider()
    {
        // Arrange & Act
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-small");

        // Assert
        provider.Name.Should().Be("OpenAI");
        provider.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void Constructor_WithLargeModel_Has3072Dimensions()
    {
        // Arrange & Act
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-large");

        // Assert
        provider.Dimensions.Should().Be(3072);
    }

    [Fact]
    public void Constructor_WithAdaModel_Has1536Dimensions()
    {
        // Arrange & Act
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-ada-002");

        // Assert
        provider.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void Constructor_WithUnknownModel_DefaultsTo1536Dimensions()
    {
        // Arrange & Act
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "unknown-model");

        // Assert
        provider.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new OpenAIEmbeddingProvider(null!, "text-embedding-3-small");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new OpenAIEmbeddingProvider("", "text-embedding-3-small");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullModel_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new OpenAIEmbeddingProvider("sk-test-key", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyText_ReturnsZeroVector()
    {
        // Arrange
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-small");

        // Act
        var result = await provider.EmbedAsync("");

        // Assert
        result.Should().HaveCount(1536);
        result.Should().OnlyContain(f => f == 0);
    }

    [Fact]
    public async Task EmbedAsync_WithWhitespaceText_ReturnsZeroVector()
    {
        // Arrange
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-small");

        // Act
        var result = await provider.EmbedAsync("   ");

        // Assert
        result.Should().HaveCount(1536);
        result.Should().OnlyContain(f => f == 0);
    }

    [Fact]
    public async Task EmbedBatchAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-small");

        // Act
        var result = await provider.EmbedBatchAsync(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsIEmbeddingProvider()
    {
        // Arrange & Act
        var provider = new OpenAIEmbeddingProvider("sk-test-key", "text-embedding-3-small");

        // Assert
        provider.Should().BeAssignableTo<IEmbeddingProvider>();
    }
}
