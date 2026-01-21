using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// VectorRetriever 单元测试
/// </summary>
public class VectorRetrieverTests
{
    private readonly Mock<IEmbeddingProvider> _embeddingProviderMock;
    private readonly Mock<IVectorStore> _vectorStoreMock;
    private readonly VectorRetriever _retriever;

    public VectorRetrieverTests()
    {
        _embeddingProviderMock = new Mock<IEmbeddingProvider>();
        _vectorStoreMock = new Mock<IVectorStore>();

        var options = Options.Create(
            new RAGOptions
            {
                TopK = 3,
                MinScore = 0.5f,
                ContextTemplate = "[{index}] {content}",
            }
        );

        _retriever = new VectorRetriever(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            options
        );
    }

    #region RetrieveAsync Tests

    [Fact]
    public async Task RetrieveAsync_ShouldEmbedQueryAndSearch()
    {
        // Arrange
        var query = "What is AI?";
        var queryEmbedding = new[] { 0.1f, 0.2f, 0.3f };
        var searchResults = new List<SearchResult>
        {
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    Id = "c1",
                    Content = "AI is artificial intelligence",
                    Embedding = queryEmbedding,
                    DocumentId = "doc1",
                    ChunkIndex = 0,
                },
                Score = 0.9f,
            },
        };

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorStoreMock
            .Setup(s =>
                s.SearchAsync(queryEmbedding, It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(searchResults);

        // Act
        var results = await _retriever.RetrieveAsync(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].Score.Should().Be(0.9f);
        _embeddingProviderMock.Verify(p => p.EmbedAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_WithCustomTopK_ShouldPassToVectorStore()
    {
        // Arrange
        var query = "test";
        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f, 0.2f]);

        _vectorStoreMock
            .Setup(s => s.SearchAsync(It.IsAny<float[]>(), 10, It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _retriever.RetrieveAsync(query, topK: 10);

        // Assert
        _vectorStoreMock.Verify(
            s => s.SearchAsync(It.IsAny<float[]>(), 10, It.IsAny<float>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region RetrieveContextAsync Tests

    [Fact]
    public async Task RetrieveContextAsync_ShouldFormatContext()
    {
        // Arrange
        var query = "What is AI?";
        var queryEmbedding = new[] { 0.1f, 0.2f };
        var searchResults = new List<SearchResult>
        {
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    Id = "c1",
                    Content = "AI is cool",
                    Embedding = queryEmbedding,
                    DocumentId = "doc1",
                    ChunkIndex = 0,
                },
                Score = 0.9f,
            },
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    Id = "c2",
                    Content = "AI is amazing",
                    Embedding = queryEmbedding,
                    DocumentId = "doc1",
                    ChunkIndex = 1,
                },
                Score = 0.8f,
            },
        };

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorStoreMock
            .Setup(s =>
                s.SearchAsync(queryEmbedding, It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(searchResults);

        // Act
        var context = await _retriever.RetrieveContextAsync(query);

        // Assert
        context.Should().Contain("AI is cool");
        context.Should().Contain("AI is amazing");
    }

    [Fact]
    public async Task RetrieveContextAsync_NoResults_ShouldReturnEmptyContext()
    {
        // Arrange
        var query = "Something unknown";
        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f]);

        _vectorStoreMock
            .Setup(s =>
                s.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        // Act
        var context = await _retriever.RetrieveContextAsync(query);

        // Assert
        context.Should().BeEmpty();
    }

    #endregion
}
