using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// SemanticSkillRouter 测试
/// </summary>
public sealed class SemanticSkillRouterTests
{
    private readonly Mock<IToolReader> _mockToolReader;
    private readonly Mock<IEmbeddingProvider> _mockEmbedding;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly SkillRouterOptions _options;
    private readonly SemanticSkillRouter _router;

    public SemanticSkillRouterTests()
    {
        _mockToolReader = new Mock<IToolReader>();
        _mockEmbedding = new Mock<IEmbeddingProvider>();
        _mockVectorStore = new Mock<IVectorStore>();
        _options = new SkillRouterOptions { ActivationThreshold = 3, DefaultTopK = 5 };

        _router = new SemanticSkillRouter(
            _mockToolReader.Object,
            _mockEmbedding.Object,
            _mockVectorStore.Object,
            Options.Create(_options)
        );
    }

    #region RouteAsync

    [Fact]
    public async Task RouteAsync_BelowThreshold_ShouldReturnAllTools()
    {
        var tools = new List<ITool> { CreateMockTool("tool1"), CreateMockTool("tool2") };
        _mockToolReader.Setup(r => r.GetAllTools()).Returns(tools);

        var result = await _router.RouteAsync("do something");

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(st => st.Score.Should().Be(1.0f));
    }

    [Fact]
    public async Task RouteAsync_AboveThreshold_ShouldUseVectorSearch()
    {
        // 4 tools (above threshold of 3)
        var tools = Enumerable.Range(1, 4).Select(i => CreateMockTool($"tool{i}")).ToList();
        _mockToolReader.Setup(r => r.GetAllTools()).Returns(tools);

        // Mock embedding
        var queryVector = new float[] { 1, 0, 0 };
        _mockEmbedding
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryVector);

        _mockEmbedding
            .Setup(e =>
                e.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                tools.Select(_ => new float[] { 1, 0, 0 }).ToList() as IReadOnlyList<float[]>
            );

        // Mock vector search results
        var searchResults = new List<SearchResult>
        {
            new()
            {
                Chunk = new DocumentChunk
                {
                    Id = "dawning-skill-router-tool1",
                    Content = "tool1",
                    Metadata = new Dictionary<string, string> { ["tool_name"] = "tool1" },
                },
                Score = 0.9f,
            },
            new()
            {
                Chunk = new DocumentChunk
                {
                    Id = "dawning-skill-router-tool3",
                    Content = "tool3",
                    Metadata = new Dictionary<string, string> { ["tool_name"] = "tool3" },
                },
                Score = 0.7f,
            },
        };

        _mockVectorStore
            .Setup(vs =>
                vs.SearchAsync(
                    It.IsAny<float[]>(),
                    It.IsAny<int>(),
                    It.IsAny<float>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(searchResults);

        var result = await _router.RouteAsync("some task", topK: 2);

        result.Should().HaveCount(2);
        result[0].Tool.Name.Should().Be("tool1");
        result[0].Score.Should().Be(0.9f);
        result[1].Tool.Name.Should().Be("tool3");
    }

    [Fact]
    public async Task RouteAsync_NullTaskDescription_ShouldThrow()
    {
        var act = () => _router.RouteAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RouteAsync_EmptyTaskDescription_ShouldThrow()
    {
        var act = () => _router.RouteAsync("   ");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region RebuildIndexAsync

    [Fact]
    public async Task RebuildIndexAsync_ShouldIndexAllTools()
    {
        var tools = new List<ITool> { CreateMockTool("t1"), CreateMockTool("t2") };
        _mockToolReader.Setup(r => r.GetAllTools()).Returns(tools);
        _mockEmbedding
            .Setup(e =>
                e.EmbedBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                tools.Select(_ => new float[] { 1, 0 }).ToList() as IReadOnlyList<float[]>
            );

        await _router.RebuildIndexAsync();

        _mockVectorStore.Verify(
            vs =>
                vs.AddBatchAsync(
                    It.Is<IEnumerable<DocumentChunk>>(chunks => chunks.Count() == 2),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullToolReader_ShouldThrow()
    {
        var act = () =>
            new SemanticSkillRouter(
                null!,
                _mockEmbedding.Object,
                _mockVectorStore.Object,
                Options.Create(_options)
            );
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEmbeddingProvider_ShouldThrow()
    {
        var act = () =>
            new SemanticSkillRouter(
                _mockToolReader.Object,
                null!,
                _mockVectorStore.Object,
                Options.Create(_options)
            );
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullVectorStore_ShouldThrow()
    {
        var act = () =>
            new SemanticSkillRouter(
                _mockToolReader.Object,
                _mockEmbedding.Object,
                null!,
                Options.Create(_options)
            );
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        var act = () =>
            new SemanticSkillRouter(
                _mockToolReader.Object,
                _mockEmbedding.Object,
                _mockVectorStore.Object,
                null!
            );
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SkillRouterOptions Validation

    [Fact]
    public void SkillRouterOptions_Valid_ShouldNotThrow()
    {
        var options = new SkillRouterOptions
        {
            ActivationThreshold = 5,
            DefaultTopK = 3,
            DefaultMinScore = 0.5f,
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void SkillRouterOptions_InvalidThreshold_ShouldThrow()
    {
        var options = new SkillRouterOptions { ActivationThreshold = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SkillRouterOptions_InvalidTopK_ShouldThrow()
    {
        var options = new SkillRouterOptions { DefaultTopK = 0 };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SkillRouterOptions_InvalidMinScore_ShouldThrow()
    {
        var options = new SkillRouterOptions { DefaultMinScore = 1.5f };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SkillRouterOptions_NegativeMinScore_ShouldThrow()
    {
        var options = new SkillRouterOptions { DefaultMinScore = -0.1f };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region ScoredTool Record

    [Fact]
    public void ScoredTool_ShouldExposeProperties()
    {
        var tool = CreateMockTool("test");
        var scored = new ScoredTool(tool, 0.85f);
        scored.Tool.Name.Should().Be("test");
        scored.Score.Should().Be(0.85f);
    }

    #endregion

    private static ITool CreateMockTool(string name)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns($"Description for {name}");
        return mock.Object;
    }
}
