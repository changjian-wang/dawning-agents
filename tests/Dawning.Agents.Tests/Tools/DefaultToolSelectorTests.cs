using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Moq;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// DefaultToolSelector 单元测试
/// </summary>
public sealed class DefaultToolSelectorTests
{
    #region SelectToolsAsync 测试

    [Fact]
    public async Task SelectToolsAsync_LessThanMaxTools_ShouldReturnAll()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = CreateMockTools(5);

        // Act
        var result = await selector.SelectToolsAsync("test query", tools, maxTools: 10);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task SelectToolsAsync_MoreThanMaxTools_ShouldReturnMaxTools()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = CreateMockTools(20);

        // Act
        var result = await selector.SelectToolsAsync("test query", tools, maxTools: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldPrioritizeExactNameMatch()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("other_tool", "Some description"),
            CreateMockTool("search", "Search tool"),
            CreateMockTool("another", "Another tool"),
        };

        // Act
        var result = await selector.SelectToolsAsync("search", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("search");
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldMatchDescription()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("tool1", "Does nothing"),
            CreateMockTool("tool2", "Calculate math expressions"),
            CreateMockTool("tool3", "Also nothing"),
        };

        // Act
        var result = await selector.SelectToolsAsync("calculate", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("tool2");
    }

    [Fact]
    public async Task SelectToolsAsync_ShouldMatchCategory()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("tool1", "Description", "Math"),
            CreateMockTool("tool2", "Description", "FileSystem"),
            CreateMockTool("tool3", "Description", "Network"),
        };

        // Act
        var result = await selector.SelectToolsAsync("filesystem", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("tool2");
    }

    [Fact]
    public async Task SelectToolsAsync_WithNullQuery_ShouldThrow()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = CreateMockTools(5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectToolsAsync(null!, tools, maxTools: 5)
        );
    }

    [Fact]
    public async Task SelectToolsAsync_WithEmptyQuery_ShouldThrow()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = CreateMockTools(5);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            selector.SelectToolsAsync("", tools, maxTools: 5)
        );
    }

    [Fact]
    public async Task SelectToolsAsync_WithNullTools_ShouldThrow()
    {
        // Arrange
        var selector = new DefaultToolSelector();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectToolsAsync("query", null!, maxTools: 5)
        );
    }

    [Fact]
    public async Task SelectToolsAsync_FileKeyword_ShouldMatchFilesystemTools()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("math_add", "Add numbers", "Math"),
            CreateMockTool("file_read", "Read file", "FileSystem"),
            CreateMockTool("network_get", "HTTP GET", "Network"),
        };

        // Act
        var result = await selector.SelectToolsAsync("file", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("file_read");
    }

    [Fact]
    public async Task SelectToolsAsync_GitKeyword_ShouldMatchGitTools()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("math_add", "Add numbers", "Math"),
            CreateMockTool("git_status", "Git status", "Git"),
            CreateMockTool("network_get", "HTTP GET", "Network"),
        };

        // Act
        var result = await selector.SelectToolsAsync("git", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("git_status");
    }

    [Fact]
    public async Task SelectToolsAsync_SearchKeyword_ShouldMatchSearchTools()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("math_add", "Add numbers", "Math"),
            CreateMockTool("grep_search", "Search in files", "Search"),
            CreateMockTool("network_get", "HTTP GET", "Network"),
        };

        // Act
        var result = await selector.SelectToolsAsync("search", tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("grep_search");
    }

    #endregion

    #region SelectToolSetsAsync 测试

    [Fact]
    public async Task SelectToolSetsAsync_LessThanMax_ShouldReturnAll()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var toolSets = CreateMockToolSets(3);

        // Act
        var result = await selector.SelectToolSetsAsync("test query", toolSets, maxToolSets: 5);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SelectToolSetsAsync_MoreThanMax_ShouldReturnMax()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var toolSets = CreateMockToolSets(10);

        // Act
        var result = await selector.SelectToolSetsAsync("test query", toolSets, maxToolSets: 3);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SelectToolSetsAsync_ShouldMatchName()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var toolSets = new List<IToolSet>
        {
            CreateMockToolSet("math", "Math tools"),
            CreateMockToolSet("git", "Git tools"),
            CreateMockToolSet("file", "File tools"),
        };

        // Act
        var result = await selector.SelectToolSetsAsync("git", toolSets, maxToolSets: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("git");
    }

    [Fact]
    public async Task SelectToolSetsAsync_WithNullQuery_ShouldThrow()
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var toolSets = CreateMockToolSets(3);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectToolSetsAsync(null!, toolSets, maxToolSets: 3)
        );
    }

    [Fact]
    public async Task SelectToolSetsAsync_WithNullToolSets_ShouldThrow()
    {
        // Arrange
        var selector = new DefaultToolSelector();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectToolSetsAsync("query", null!, maxToolSets: 3)
        );
    }

    #endregion

    #region 关键词匹配测试

    [Theory]
    [InlineData("文件", "file_read", "FileSystem")]
    [InlineData("搜索", "search_tool", "Search")]
    [InlineData("查找", "find_tool", "Search")]
    [InlineData("提交", "git_commit", "Git")]
    [InlineData("push", "git_push", "Git")]
    public async Task SelectToolsAsync_ChineseKeywords_ShouldMatch(
        string query,
        string toolName,
        string category
    )
    {
        // Arrange
        var selector = new DefaultToolSelector();
        var tools = new List<ITool>
        {
            CreateMockTool("other_tool", "Description", "Other"),
            CreateMockTool(toolName, "Description", category),
        };

        // Act
        var result = await selector.SelectToolsAsync(query, tools, maxTools: 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be(toolName);
    }

    #endregion

    #region 辅助方法

    private static IReadOnlyList<ITool> CreateMockTools(int count)
    {
        var tools = new List<ITool>();
        for (int i = 0; i < count; i++)
        {
            tools.Add(CreateMockTool($"tool_{i}", $"Description for tool {i}"));
        }
        return tools;
    }

    private static ITool CreateMockTool(string name, string description, string? category = null)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.Category).Returns(category);
        return mock.Object;
    }

    private static IReadOnlyList<IToolSet> CreateMockToolSets(int count)
    {
        var toolSets = new List<IToolSet>();
        for (int i = 0; i < count; i++)
        {
            toolSets.Add(CreateMockToolSet($"toolset_{i}", $"Description for toolset {i}"));
        }
        return toolSets;
    }

    private static IToolSet CreateMockToolSet(string name, string description)
    {
        var mock = new Mock<IToolSet>();
        mock.Setup(ts => ts.Name).Returns(name);
        mock.Setup(ts => ts.Description).Returns(description);
        mock.Setup(ts => ts.Tools).Returns(Array.Empty<ITool>());
        return mock.Object;
    }

    #endregion
}
