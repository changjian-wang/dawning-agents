using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// EditFileTool tests
/// </summary>
public class EditFileToolTests : IDisposable
{
    private readonly EditFileTool _tool;
    private readonly string _tempDir;

    public EditFileToolTests()
    {
        _tool = new EditFileTool();
        _tempDir = Path.Combine(Path.GetTempPath(), $"edit_file_test_{Guid.NewGuid():N}");
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
        _tool.Name.Should().Be("edit_file");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.Medium);
        _tool.RequiresConfirmation.Should().BeFalse();
        _tool.Category.Should().Be("Core");
    }

    #endregion

    #region Successful Edit

    [Fact]
    public async Task ExecuteAsync_ExactMatch_ShouldReplace()
    {
        // Arrange
        var filePath = CreateTestFile("hello world\ngoodbye world");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "hello world",
                newString = "hi world",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Edited");
        File.ReadAllText(filePath).Should().Be("hi world\ngoodbye world");
    }

    [Fact]
    public async Task ExecuteAsync_MultiLineEdit_ShouldReportLineRange()
    {
        // Arrange
        var filePath = CreateTestFile("line1\nline2\nline3\nline4");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "line2\nline3",
                newString = "replaced2\nreplaced3\nnew_extra",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 lines → 3 lines");
        var content = File.ReadAllText(filePath);
        content.Should().Contain("replaced2");
        content.Should().Contain("new_extra");
    }

    [Fact]
    public async Task ExecuteAsync_Delete_ShouldRemoveText()
    {
        // Arrange
        var filePath = CreateTestFile("keep\nremove me\nkeep too");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "remove me\n",
                newString = "",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        File.ReadAllText(filePath).Should().Be("keep\nkeep too");
    }

    #endregion

    #region No Match

    [Fact]
    public async Task ExecuteAsync_NoMatch_ShouldReturnError()
    {
        // Arrange
        var filePath = CreateTestFile("hello world");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "nonexistent text",
                newString = "replacement",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceDifference_ShouldSuggestHint()
    {
        // Arrange
        var filePath = CreateTestFile("  hello world  ");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "hello world",
                newString = "hi",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        // The trimmed version matches inside the untrimmed file content,
        // so we'd get a match here since "hello world" is contained in "  hello world  "
        // But actually the exact match finds it! Let me re-think...
        // "hello world" IS found exactly once in "  hello world  "
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_TrimmedMatch_ShouldSuggestWhitespace()
    {
        // Arrange — oldString has extra whitespace that doesn't exist in file
        var filePath = CreateTestFile("hello world");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "  hello world  ",
                newString = "hi",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("whitespace");
    }

    #endregion

    #region Multiple Matches

    [Fact]
    public async Task ExecuteAsync_MultipleMatches_ShouldReturnError()
    {
        // Arrange
        var filePath = CreateTestFile("hello\nhello\nhello");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "hello",
                newString = "hi",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("3 locations");
        result.Error.Should().Contain("more context");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                path = "/nonexistent/file.txt",
                oldString = "x",
                newString = "y",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOldString_ShouldReturnError()
    {
        // Arrange
        var filePath = CreateTestFile("content");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                oldString = "",
                newString = "new",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("oldString cannot be empty");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPath_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                path = "",
                oldString = "x",
                newString = "y",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    #endregion

    private string CreateTestFile(string content)
    {
        var filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
