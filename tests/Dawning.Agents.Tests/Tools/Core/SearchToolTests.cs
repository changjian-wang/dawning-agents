using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// SearchTool 测试
/// </summary>
public class SearchToolTests : IDisposable
{
    private readonly SearchTool _tool;
    private readonly string _tempDir;

    public SearchToolTests()
    {
        _tool = new SearchTool();
        _tempDir = Path.Combine(Path.GetTempPath(), $"search_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region Tool Properties

    [Fact]
    public void Properties_ShouldBeCorrect()
    {
        _tool.Name.Should().Be("search");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.Low);
        _tool.RequiresConfirmation.Should().BeFalse();
        _tool.Category.Should().Be("Core");
    }

    #endregion

    #region Grep Mode

    [Fact]
    public async Task GrepSearch_PlainText_ShouldFindMatches()
    {
        // Arrange
        CreateTestFile("foo.txt", "hello world\ngoodbye world\nhello again");
        var input = JsonSerializer.Serialize(new { pattern = "hello", path = _tempDir });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 match");
        result.Output.Should().Contain("hello world");
        result.Output.Should().Contain("hello again");
    }

    [Fact]
    public async Task GrepSearch_CaseInsensitive_ShouldMatch()
    {
        // Arrange
        CreateTestFile("test.txt", "Hello World\nHELLO WORLD");
        var input = JsonSerializer.Serialize(new { pattern = "hello", path = _tempDir });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 match");
    }

    [Fact]
    public async Task GrepSearch_Regex_ShouldMatch()
    {
        // Arrange
        CreateTestFile("code.cs", "int x = 42;\nstring y = \"hello\";\nint z = 99;");
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = @"int \w+ = \d+",
                path = _tempDir,
                isRegex = true,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 match");
    }

    [Fact]
    public async Task GrepSearch_InvalidRegex_ShouldReturnError()
    {
        // Arrange
        CreateTestFile("test.txt", "content");
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = "[invalid",
                path = _tempDir,
                isRegex = true,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid regex");
    }

    [Fact]
    public async Task GrepSearch_NoMatches_ShouldReturnNoResults()
    {
        // Arrange
        CreateTestFile("test.txt", "hello world");
        var input = JsonSerializer.Serialize(new { pattern = "zzz_nonexistent", path = _tempDir });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("No matches");
    }

    [Fact]
    public async Task GrepSearch_IncludePattern_ShouldFilterFiles()
    {
        // Arrange
        CreateTestFile("code.cs", "hello from cs");
        CreateTestFile("readme.md", "hello from md");
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = "hello",
                path = _tempDir,
                includePattern = "*.cs",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("1 match");
        result.Output.Should().Contain("code.cs");
    }

    [Fact]
    public async Task GrepSearch_MaxResults_ShouldTruncate()
    {
        // Arrange
        var lines = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"match_line_{i}"));
        CreateTestFile("big.txt", lines);
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = "match_line",
                path = _tempDir,
                maxResults = 5,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("5 match");
        result.Output.Should().Contain("truncated");
    }

    [Fact]
    public async Task GrepSearch_ShowsLineNumbers()
    {
        // Arrange
        CreateTestFile("test.txt", "aaa\nbbb\nccc\nbbb\neee");
        var input = JsonSerializer.Serialize(new { pattern = "bbb", path = _tempDir });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2:");
        result.Output.Should().Contain("4:");
    }

    #endregion

    #region Glob Mode

    [Fact]
    public async Task GlobSearch_ShouldFindFiles()
    {
        // Arrange
        CreateTestFile("a.cs", "");
        CreateTestFile("b.cs", "");
        CreateTestFile("c.txt", "");
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = "*.cs",
                mode = "glob",
                path = _tempDir,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 file(s)");
        result.Output.Should().Contain("a.cs");
        result.Output.Should().Contain("b.cs");
    }

    [Fact]
    public async Task GlobSearch_NoMatch_ShouldReturnNoResults()
    {
        // Arrange
        CreateTestFile("test.txt", "");
        var input = JsonSerializer.Serialize(
            new
            {
                pattern = "*.py",
                mode = "glob",
                path = _tempDir,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("No files found");
    }

    #endregion

    #region Ignored Paths

    [Fact]
    public async Task GrepSearch_ShouldSkipIgnoredDirectories()
    {
        // Arrange
        var nodeModulesDir = Path.Combine(_tempDir, "node_modules");
        Directory.CreateDirectory(nodeModulesDir);
        File.WriteAllText(Path.Combine(nodeModulesDir, "lib.js"), "target_string");
        CreateTestFile("src.js", "target_string");

        var input = JsonSerializer.Serialize(new { pattern = "target_string", path = _tempDir });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("1 match");
        result.Output.Should().Contain("src.js");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_EmptyPattern_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { pattern = "" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ExecuteAsync_DirectoryNotFound_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { pattern = "test", path = "/nonexistent/dir" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Directory not found");
    }

    #endregion

    private void CreateTestFile(string name, string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, name), content);
    }
}
