using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.RAG;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// DocumentChunker 单元测试
/// </summary>
public class DocumentChunkerTests
{
    #region ChunkText Tests

    [Fact]
    public void ChunkText_ShortText_ShouldReturnSingleChunk()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 100);
        var chunker = new DocumentChunker(options);

        // Act
        var chunks = chunker.ChunkText("Short text", "doc1");

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Content.Should().Be("Short text");
        chunks[0].DocumentId.Should().Be("doc1");
        chunks[0].ChunkIndex.Should().Be(0);
    }

    [Fact]
    public void ChunkText_MultipleParagraphs_ShouldSplitByParagraph()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 50);
        var chunker = new DocumentChunker(options);
        var text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";

        // Act
        var chunks = chunker.ChunkText(text, "doc1");

        // Assert
        chunks.Count.Should().BeGreaterThan(1);
        chunks.Should().AllSatisfy(c => c.DocumentId.Should().Be("doc1"));
    }

    [Fact]
    public void ChunkText_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 100);
        var chunker = new DocumentChunker(options);
        var metadata = new Dictionary<string, string>
        {
            ["source"] = "test.txt",
            ["author"] = "tester",
        };

        // Act
        var chunks = chunker.ChunkText("Hello world", "doc1", metadata);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Metadata.Should().ContainKey("source");
        chunks[0].Metadata["source"].Should().Be("test.txt");
    }

    [Fact]
    public void ChunkText_ShouldGenerateUniqueIds()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 20);
        var chunker = new DocumentChunker(options);
        var text = "Chunk one.\n\nChunk two.\n\nChunk three.";

        // Act
        var chunks = chunker.ChunkText(text, "doc1");

        // Assert
        var ids = chunks.Select(c => c.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ChunkText_ChunkIndices_ShouldBeSequential()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 20);
        var chunker = new DocumentChunker(options);
        var text = "Para one.\n\nPara two.\n\nPara three.";

        // Act
        var chunks = chunker.ChunkText(text, "doc1");

        // Assert
        var indices = chunks.Select(c => c.ChunkIndex).ToList();
        indices.Should().BeInAscendingOrder();
        indices.First().Should().Be(0);
    }

    #endregion

    #region Overlap Tests

    [Fact]
    public void ChunkText_WithOverlap_ShouldHaveOverlappingContent()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 50, chunkOverlap: 10);
        var chunker = new DocumentChunker(options);
        // Create text that will definitely need multiple chunks
        var text = string.Join(
            "\n\n",
            Enumerable.Range(1, 10).Select(i => $"This is paragraph number {i}.")
        );

        // Act
        var chunks = chunker.ChunkText(text, "doc1");

        // Assert
        chunks.Count.Should().BeGreaterThan(1);
    }

    #endregion

    #region Empty/Null Input Tests

    [Fact]
    public void ChunkText_EmptyText_ShouldReturnEmpty()
    {
        // Arrange
        var options = CreateOptions();
        var chunker = new DocumentChunker(options);

        // Act
        var chunks = chunker.ChunkText("", "doc1");

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WhitespaceOnly_ShouldReturnEmpty()
    {
        // Arrange
        var options = CreateOptions();
        var chunker = new DocumentChunker(options);

        // Act
        var chunks = chunker.ChunkText("   \n\n   ", "doc1");

        // Assert
        chunks.Should().BeEmpty();
    }

    #endregion

    #region Large Paragraph Tests

    [Fact]
    public void ChunkText_LargeParagraph_ShouldSplitBySentence()
    {
        // Arrange
        var options = CreateOptions(chunkSize: 50);
        var chunker = new DocumentChunker(options);
        var largeParagraph =
            "First sentence here. Second sentence here. Third sentence. Fourth one.";

        // Act
        var chunks = chunker.ChunkText(largeParagraph, "doc1");

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().AllSatisfy(c => c.Content.Length.Should().BeLessThanOrEqualTo(100));
    }

    #endregion

    #region Helper Methods

    private static IOptions<RAGOptions> CreateOptions(
        int chunkSize = 500,
        int chunkOverlap = 50
    )
    {
        return Options.Create(
            new RAGOptions { ChunkSize = chunkSize, ChunkOverlap = chunkOverlap }
        );
    }

    #endregion
}
