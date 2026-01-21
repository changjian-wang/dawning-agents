using Dawning.Agents.Abstractions.RAG;
using FluentAssertions;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// InMemoryVectorStore 单元测试
/// </summary>
public class InMemoryVectorStoreTests
{
    private readonly IVectorStore _store;

    public InMemoryVectorStoreTests()
    {
        _store = new Dawning.Agents.Core.RAG.InMemoryVectorStore();
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_ShouldStoreChunk()
    {
        // Arrange
        var chunk = CreateChunk("chunk1", "Hello world", [0.1f, 0.2f, 0.3f]);

        // Act
        await _store.AddAsync(chunk);
        var retrieved = await _store.GetAsync("chunk1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task AddAsync_ShouldUpdateExistingChunk()
    {
        // Arrange
        var chunk1 = CreateChunk("chunk1", "Original", [0.1f, 0.2f, 0.3f]);
        var chunk2 = CreateChunk("chunk1", "Updated", [0.4f, 0.5f, 0.6f]);

        // Act
        await _store.AddAsync(chunk1);
        await _store.AddAsync(chunk2);
        var retrieved = await _store.GetAsync("chunk1");

        // Assert
        retrieved!.Content.Should().Be("Updated");
    }

    #endregion

    #region AddBatchAsync Tests

    [Fact]
    public async Task AddBatchAsync_ShouldStoreMultipleChunks()
    {
        // Arrange
        var chunks = new[]
        {
            CreateChunk("c1", "Content 1", [0.1f, 0.2f, 0.3f]),
            CreateChunk("c2", "Content 2", [0.4f, 0.5f, 0.6f]),
            CreateChunk("c3", "Content 3", [0.7f, 0.8f, 0.9f]),
        };

        // Act
        await _store.AddBatchAsync(chunks);

        // Assert
        (await _store.GetAsync("c1")).Should().NotBeNull();
        (await _store.GetAsync("c2")).Should().NotBeNull();
        (await _store.GetAsync("c3")).Should().NotBeNull();
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ShouldReturnSimilarChunks()
    {
        // Arrange
        await _store.AddBatchAsync(
        [
            CreateChunk("c1", "Apple", [1.0f, 0.0f, 0.0f]),
            CreateChunk("c2", "Banana", [0.0f, 1.0f, 0.0f]),
            CreateChunk("c3", "Cherry", [0.0f, 0.0f, 1.0f]),
        ]);

        // Query similar to Apple
        var queryEmbedding = new[] { 0.9f, 0.1f, 0.0f };

        // Act
        var results = await _store.SearchAsync(queryEmbedding, topK: 2);

        // Assert
        results.Should().HaveCount(2);
        results[0].Chunk.Content.Should().Be("Apple");
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectMinScore()
    {
        // Arrange
        await _store.AddBatchAsync(
        [
            CreateChunk("c1", "Similar", [1.0f, 0.0f, 0.0f]),
            CreateChunk("c2", "Different", [0.0f, 1.0f, 0.0f]),
        ]);

        var queryEmbedding = new[] { 1.0f, 0.0f, 0.0f };

        // Act - high min score should filter out different chunk
        var results = await _store.SearchAsync(queryEmbedding, topK: 10, minScore: 0.9f);

        // Assert
        results.Should().HaveCount(1);
        results[0].Chunk.Content.Should().Be("Similar");
    }

    [Fact]
    public async Task SearchAsync_EmptyStore_ShouldReturnEmpty()
    {
        // Act
        var results = await _store.SearchAsync([1.0f, 0.0f, 0.0f]);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnScoresInDescendingOrder()
    {
        // Arrange
        await _store.AddBatchAsync(
        [
            CreateChunk("c1", "A", [1.0f, 0.0f]),
            CreateChunk("c2", "B", [0.5f, 0.5f]),
            CreateChunk("c3", "C", [0.0f, 1.0f]),
        ]);

        // Act
        var results = await _store.SearchAsync([1.0f, 0.0f], topK: 3);

        // Assert
        results.Should().BeInDescendingOrder(r => r.Score);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveChunk()
    {
        // Arrange
        await _store.AddAsync(CreateChunk("c1", "Content", [0.1f, 0.2f]));

        // Act
        var deleted = await _store.DeleteAsync("c1");
        var retrieved = await _store.GetAsync("c1");

        // Assert
        deleted.Should().BeTrue();
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ShouldReturnFalse()
    {
        // Act
        var deleted = await _store.DeleteAsync("nonexistent");

        // Assert
        deleted.Should().BeFalse();
    }

    #endregion

    #region DeleteByDocumentIdAsync Tests

    [Fact]
    public async Task DeleteByDocumentIdAsync_ShouldRemoveAllChunksForDocument()
    {
        // Arrange
        await _store.AddBatchAsync(
        [
            CreateChunk("c1", "Chunk 1", [0.1f, 0.2f], "doc1"),
            CreateChunk("c2", "Chunk 2", [0.3f, 0.4f], "doc1"),
            CreateChunk("c3", "Chunk 3", [0.5f, 0.6f], "doc2"),
        ]);

        // Act
        var deletedCount = await _store.DeleteByDocumentIdAsync("doc1");

        // Assert
        deletedCount.Should().Be(2);
        (await _store.GetAsync("c1")).Should().BeNull();
        (await _store.GetAsync("c2")).Should().BeNull();
        (await _store.GetAsync("c3")).Should().NotBeNull();
    }

    #endregion

    #region ClearAsync Tests

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllChunks()
    {
        // Arrange
        await _store.AddBatchAsync(
        [
            CreateChunk("c1", "A", [0.1f]),
            CreateChunk("c2", "B", [0.2f]),
        ]);

        // Act
        await _store.ClearAsync();
        var results = await _store.SearchAsync([0.1f], topK: 10);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static DocumentChunk CreateChunk(
        string id,
        string content,
        float[] embedding,
        string? documentId = null
    )
    {
        return new DocumentChunk
        {
            Id = id,
            Content = content,
            Embedding = embedding,
            Metadata = new Dictionary<string, string>(),
            DocumentId = documentId,
            ChunkIndex = 0,
        };
    }

    #endregion
}
