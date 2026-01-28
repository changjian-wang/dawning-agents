using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Azure;
using FluentAssertions;

namespace Dawning.Agents.Tests.RAG;

public class AzureOpenAIEmbeddingProviderTests
{
    [Fact]
    public void Constructor_WithValidParams_CreatesProvider()
    {
        // Arrange & Act
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment"
        );

        // Assert
        provider.Name.Should().Be("AzureOpenAI");
        provider.Dimensions.Should().Be(1536);
    }

    [Fact]
    public void Constructor_WithCustomDimensions_UsesProvidedDimensions()
    {
        // Arrange & Act
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment",
            dimensions: 3072
        );

        // Assert
        provider.Dimensions.Should().Be(3072);
    }

    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new AzureOpenAIEmbeddingProvider(
            null!,
            "test-key",
            "embedding-deployment"
        );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new AzureOpenAIEmbeddingProvider(
            "",
            "test-key",
            "embedding-deployment"
        );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            null!,
            "embedding-deployment"
        );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullDeploymentName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            null!
        );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyText_ReturnsZeroVector()
    {
        // Arrange
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment"
        );

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
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment"
        );

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
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment"
        );

        // Act
        var result = await provider.EmbedBatchAsync(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ImplementsIEmbeddingProvider()
    {
        // Arrange & Act
        var provider = new AzureOpenAIEmbeddingProvider(
            "https://test.openai.azure.com",
            "test-key",
            "embedding-deployment"
        );

        // Assert
        provider.Should().BeAssignableTo<IEmbeddingProvider>();
    }
}
