using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// ReadFileTool 测试
/// </summary>
public class ReadFileToolTests : IDisposable
{
    private readonly ReadFileTool _tool;
    private readonly string _tempDir;

    public ReadFileToolTests()
    {
        _tool = new ReadFileTool();
        _tempDir = Path.Combine(Path.GetTempPath(), $"read_file_test_{Guid.NewGuid():N}");
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
        _tool.Name.Should().Be("read_file");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.Low);
        _tool.RequiresConfirmation.Should().BeFalse();
        _tool.Category.Should().Be("Core");
        _tool.Description.Should().Contain("Read");
    }

    [Fact]
    public void ParametersSchema_ShouldBeValidJson()
    {
        var act = () => JsonDocument.Parse(_tool.ParametersSchema);
        act.Should().NotThrow();
    }

    #endregion

    #region Basic Reading

    [Fact]
    public async Task ExecuteAsync_ShouldReadFileWithLineNumbers()
    {
        // Arrange
        var filePath = CreateTestFile("line1\nline2\nline3");
        var input = JsonSerializer.Serialize(new { path = filePath });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("1 | line1");
        result.Output.Should().Contain("2 | line2");
        result.Output.Should().Contain("3 | line3");
    }

    [Fact]
    public async Task ExecuteAsync_PlainStringInput_ShouldReadFile()
    {
        // Arrange
        var filePath = CreateTestFile("hello world");

        // Act
        var result = await _tool.ExecuteAsync(filePath);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFile_ShouldReturnEmpty()
    {
        // Arrange
        var filePath = CreateTestFile("");
        var input = JsonSerializer.Serialize(new { path = filePath });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Offset and Limit

    [Fact]
    public async Task ExecuteAsync_WithOffset_ShouldStartFromSpecifiedLine()
    {
        // Arrange
        var filePath = CreateTestFile("line1\nline2\nline3\nline4\nline5");
        var input = JsonSerializer.Serialize(new { path = filePath, offset = 3 });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("line3");
        result.Output.Should().Contain("line4");
        result.Output.Should().Contain("line5");
        result.Output.Should().NotContain("line1");
        result.Output.Should().NotContain("line2");
    }

    [Fact]
    public async Task ExecuteAsync_WithLimit_ShouldLimitLines()
    {
        // Arrange
        var filePath = CreateTestFile("line1\nline2\nline3\nline4\nline5");
        var input = JsonSerializer.Serialize(new { path = filePath, limit = 2 });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("line1");
        result.Output.Should().Contain("line2");
        result.Output.Should().NotContain("line3");
    }

    [Fact]
    public async Task ExecuteAsync_WithOffsetAndLimit_ShouldReturnCorrectRange()
    {
        // Arrange
        var filePath = CreateTestFile("a\nb\nc\nd\ne");
        var input = JsonSerializer.Serialize(
            new
            {
                path = filePath,
                offset = 2,
                limit = 2,
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("b");
        result.Output.Should().Contain("c");
        result.Output.Should().NotContain("| a");
        result.Output.Should().NotContain("| d");
    }

    [Fact]
    public async Task ExecuteAsync_OffsetBeyondEnd_ShouldReturnMessage()
    {
        // Arrange
        var filePath = CreateTestFile("line1\nline2");
        var input = JsonSerializer.Serialize(new { path = filePath, offset = 100 });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("2 lines");
        result.Output.Should().Contain("beyond");
    }

    [Fact]
    public async Task ExecuteAsync_FileWithMoreLines_ShouldShowContinuationHint()
    {
        // Arrange
        var lines = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"line{i}"));
        var filePath = CreateTestFile(lines);
        var input = JsonSerializer.Serialize(new { path = filePath, limit = 5 });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Showing lines");
        result.Output.Should().Contain("offset=6");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { path = "/nonexistent/file.txt" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("File not found");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPath_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { path = "" });

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
