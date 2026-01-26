using Dawning.Agents.Abstractions.RAG;
using FluentAssertions;

namespace Dawning.Agents.Tests.RAG;

/// <summary>
/// RAGOptions 单元测试
/// </summary>
public class RAGOptionsTests
{
    #region Default Values Tests

    [Fact]
    public void Default_ChunkSize_Is500()
    {
        var options = new RAGOptions();
        options.ChunkSize.Should().Be(500);
    }

    [Fact]
    public void Default_ChunkOverlap_Is50()
    {
        var options = new RAGOptions();
        options.ChunkOverlap.Should().Be(50);
    }

    [Fact]
    public void Default_TopK_Is5()
    {
        var options = new RAGOptions();
        options.TopK.Should().Be(5);
    }

    [Fact]
    public void Default_MinScore_Is0Point5()
    {
        var options = new RAGOptions();
        options.MinScore.Should().Be(0.5f);
    }

    [Fact]
    public void Default_EmbeddingModel_IsTextEmbedding3Small()
    {
        var options = new RAGOptions();
        options.EmbeddingModel.Should().Be("text-embedding-3-small");
    }

    [Fact]
    public void Default_IncludeMetadata_IsTrue()
    {
        var options = new RAGOptions();
        options.IncludeMetadata.Should().BeTrue();
    }

    [Fact]
    public void Default_ContextTemplate_ContainsIndexAndContent()
    {
        var options = new RAGOptions();
        options.ContextTemplate.Should().Contain("{index}");
        options.ContextTemplate.Should().Contain("{content}");
    }

    [Fact]
    public void SectionName_IsRAG()
    {
        RAGOptions.SectionName.Should().Be("RAG");
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        var options = new RAGOptions
        {
            ChunkSize = 1000,
            ChunkOverlap = 100,
            TopK = 10,
            MinScore = 0.7f,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidChunkSize_ThrowsException(int chunkSize)
    {
        var options = new RAGOptions { ChunkSize = chunkSize };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ChunkSize*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_NegativeChunkOverlap_ThrowsException(int chunkOverlap)
    {
        var options = new RAGOptions { ChunkOverlap = chunkOverlap };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ChunkOverlap*");
    }

    [Fact]
    public void Validate_ChunkOverlapGreaterOrEqualToChunkSize_ThrowsException()
    {
        var options = new RAGOptions
        {
            ChunkSize = 100,
            ChunkOverlap = 100, // Equal to ChunkSize
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*ChunkOverlap*");
    }

    [Fact]
    public void Validate_ChunkOverlapLessThanChunkSize_DoesNotThrow()
    {
        var options = new RAGOptions
        {
            ChunkSize = 100,
            ChunkOverlap = 99, // Just under ChunkSize
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidTopK_ThrowsException(int topK)
    {
        var options = new RAGOptions { TopK = topK };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*TopK*");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(2.0f)]
    public void Validate_InvalidMinScore_ThrowsException(float minScore)
    {
        var options = new RAGOptions { MinScore = minScore };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*MinScore*");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Validate_ValidMinScore_DoesNotThrow(float minScore)
    {
        var options = new RAGOptions { MinScore = minScore };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    #endregion

    #region Property Setting Tests

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new RAGOptions
        {
            ChunkSize = 1000,
            ChunkOverlap = 200,
            TopK = 10,
            MinScore = 0.8f,
            EmbeddingModel = "custom-model",
            IncludeMetadata = false,
            ContextTemplate = "{content} [{score}]",
        };

        options.ChunkSize.Should().Be(1000);
        options.ChunkOverlap.Should().Be(200);
        options.TopK.Should().Be(10);
        options.MinScore.Should().Be(0.8f);
        options.EmbeddingModel.Should().Be("custom-model");
        options.IncludeMetadata.Should().BeFalse();
        options.ContextTemplate.Should().Be("{content} [{score}]");
    }

    #endregion
}
