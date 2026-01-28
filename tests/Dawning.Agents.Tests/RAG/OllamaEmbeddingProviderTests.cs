using System.Net;
using System.Text.Json;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Moq;
using Moq.Protected;

namespace Dawning.Agents.Tests.RAG;

public class OllamaEmbeddingProviderTests
{
    [Fact]
    public void Constructor_WithValidParams_CreatesProvider()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Assert
        provider.Name.Should().Be("Ollama");
        provider.Dimensions.Should().Be(768);
    }

    [Theory]
    [InlineData("nomic-embed-text", 768)]
    [InlineData("mxbai-embed-large", 1024)]
    [InlineData("all-minilm", 384)]
    [InlineData("snowflake-arctic-embed", 1024)]
    [InlineData("bge-m3", 1024)]
    public void Constructor_WithKnownModel_UsesCorrectDimensions(
        string model,
        int expectedDimensions
    )
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var provider = new OllamaEmbeddingProvider(httpClient, model);

        // Assert
        provider.Dimensions.Should().Be(expectedDimensions);
    }

    [Fact]
    public void Constructor_WithModelVersionTag_UsesCorrectDimensions()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text:latest");

        // Assert
        provider.Dimensions.Should().Be(768);
    }

    [Fact]
    public void Constructor_WithUnknownModel_DefaultsTo768Dimensions()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var provider = new OllamaEmbeddingProvider(httpClient, "unknown-model");

        // Assert
        provider.Dimensions.Should().Be(768);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new OllamaEmbeddingProvider(null!, "nomic-embed-text");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullModel_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var act = () => new OllamaEmbeddingProvider(httpClient, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyModel_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var act = () => new OllamaEmbeddingProvider(httpClient, "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyText_ReturnsZeroVector()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Act
        var result = await provider.EmbedAsync("");

        // Assert
        result.Should().HaveCount(768);
        result.Should().OnlyContain(f => f == 0);
    }

    [Fact]
    public async Task EmbedAsync_WithWhitespaceText_ReturnsZeroVector()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Act
        var result = await provider.EmbedAsync("   ");

        // Assert
        result.Should().HaveCount(768);
        result.Should().OnlyContain(f => f == 0);
    }

    [Fact]
    public async Task EmbedAsync_WithValidText_CallsOllamaApi()
    {
        // Arrange
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var responseJson = JsonSerializer.Serialize(
            new { model = "nomic-embed-text", embeddings = new[] { expectedEmbedding } }
        );

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };

        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Act
        var result = await provider.EmbedAsync("test text");

        // Assert
        result.Should().BeEquivalentTo(expectedEmbedding);

        mockHandler
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post && req.RequestUri!.PathAndQuery == "/api/embed"
                ),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Fact]
    public async Task EmbedBatchAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Act
        var result = await provider.EmbedBatchAsync(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EmbedBatchAsync_WithAllEmptyTexts_ReturnsZeroVectors()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Act
        var result = await provider.EmbedBatchAsync(new[] { "", "   ", null! });

        // Assert
        result.Should().HaveCount(3);
        result
            .Should()
            .AllSatisfy(e =>
            {
                e.Should().HaveCount(768);
                e.Should().OnlyContain(f => f == 0);
            });
    }

    [Fact]
    public void ImplementsIEmbeddingProvider()
    {
        // Arrange
        var httpClient = CreateMockHttpClient();

        // Act
        var provider = new OllamaEmbeddingProvider(httpClient, "nomic-embed-text");

        // Assert
        provider.Should().BeAssignableTo<IEmbeddingProvider>();
    }

    private static HttpClient CreateMockHttpClient()
    {
        return new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
    }
}
