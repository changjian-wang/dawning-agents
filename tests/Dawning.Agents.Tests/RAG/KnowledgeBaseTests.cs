using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// KnowledgeBase 单元测试
/// </summary>
public class KnowledgeBaseTests
{
    private readonly Mock<IEmbeddingProvider> _embeddingProviderMock;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunker _chunker;
    private readonly KnowledgeBase _kb;

    public KnowledgeBaseTests()
    {
        _embeddingProviderMock = new Mock<IEmbeddingProvider>();
        _vectorStore = new InMemoryVectorStore();
        var ragOptions = Options.Create(new RAGOptions { ChunkSize = 100, ChunkOverlap = 10 });
        _chunker = new DocumentChunker(ragOptions);

        _kb = new KnowledgeBase(
            _embeddingProviderMock.Object,
            _vectorStore,
            _chunker,
            ragOptions
        );
    }

    #region AddDocumentAsync Tests

    [Fact]
    public async Task AddDocumentAsync_ShouldChunkAndEmbed()
    {
        // Arrange
        var content = "Hello world. This is a test document.";
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => new[] { 0.1f, 0.2f, 0.3f }).ToList()
            );

        // Act
        var chunkCount = await _kb.AddDocumentAsync(content, "doc1");

        // Assert
        chunkCount.Should().BeGreaterThan(0);
        _embeddingProviderMock.Verify(
            p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AddDocumentAsync_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var content = "Test content";
        var metadata = new Dictionary<string, string> { ["author"] = "test" };
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => new[] { 0.1f }).ToList()
            );

        // Act
        await _kb.AddDocumentAsync(content, "doc1", metadata);

        // Assert - verify through search
        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f]);

        var results = await _kb.QueryAsync("Test");
        results.Should().NotBeEmpty();
        results[0].Chunk.Metadata.Should().ContainKey("author");
    }

    [Fact]
    public async Task AddDocumentAsync_EmptyContent_ShouldReturnZero()
    {
        // Act
        var chunkCount = await _kb.AddDocumentAsync("", "doc1");

        // Assert
        chunkCount.Should().Be(0);
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_ShouldReturnRelevantChunks()
    {
        // Arrange
        var embedding = new[] { 1.0f, 0.0f, 0.0f };
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => embedding).ToList()
            );

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        await _kb.AddDocumentAsync("Relevant content about AI", "doc1");

        // Act
        var results = await _kb.QueryAsync("What is AI?");

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_EmptyKnowledgeBase_ShouldReturnEmpty()
    {
        // Arrange
        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f]);

        // Act
        var results = await _kb.QueryAsync("Any query");

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region QueryContextAsync Tests

    [Fact]
    public async Task QueryContextAsync_ShouldReturnFormattedContext()
    {
        // Arrange
        var embedding = new[] { 1.0f };
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => embedding).ToList()
            );

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        await _kb.AddDocumentAsync("AI stands for Artificial Intelligence", "doc1");

        // Act
        var context = await _kb.QueryContextAsync("What is AI?");

        // Assert
        context.Should().Contain("AI");
        context.Should().Contain("Artificial Intelligence");
    }

    #endregion

    #region DeleteDocumentAsync Tests

    [Fact]
    public async Task DeleteDocumentAsync_ShouldRemoveDocument()
    {
        // Arrange
        var embedding = new[] { 1.0f };
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => embedding).ToList()
            );

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        await _kb.AddDocumentAsync("Some content", "doc1");

        // Act
        var deletedCount = await _kb.DeleteDocumentAsync("doc1");

        // Assert
        deletedCount.Should().BeGreaterThan(0);

        // Verify document is gone
        var results = await _kb.QueryAsync("Some content");
        results.Should().BeEmpty();
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllDocuments()
    {
        // Arrange
        var embedding = new[] { 1.0f };
        _embeddingProviderMock
            .Setup(p => p.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => embedding).ToList()
            );

        _embeddingProviderMock
            .Setup(p => p.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        await _kb.AddDocumentAsync("Content 1", "doc1");
        await _kb.AddDocumentAsync("Content 2", "doc2");

        // Act
        await _kb.ClearAsync();

        // Assert
        var results = await _kb.QueryAsync("Content");
        results.Should().BeEmpty();
    }

    #endregion
}
