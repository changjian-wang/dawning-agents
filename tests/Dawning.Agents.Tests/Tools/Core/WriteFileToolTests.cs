using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// WriteFileTool 测试
/// </summary>
public class WriteFileToolTests : IDisposable
{
    private readonly WriteFileTool _tool;
    private readonly string _tempDir;

    public WriteFileToolTests()
    {
        _tool = new WriteFileTool();
        _tempDir = Path.Combine(Path.GetTempPath(), $"write_file_test_{Guid.NewGuid():N}");
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
        _tool.Name.Should().Be("write_file");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.Medium);
        _tool.RequiresConfirmation.Should().BeFalse();
        _tool.Category.Should().Be("Core");
    }

    #endregion

    #region Create New File

    [Fact]
    public async Task ExecuteAsync_NewFile_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "new_file.txt");
        var input = JsonSerializer.Serialize(new { path = filePath, content = "hello world" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Created");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Be("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_NewFile_ShouldReportLineCount()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "multi.txt");
        var content = "line1\nline2\nline3";
        var input = JsonSerializer.Serialize(new { path = filePath, content });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("3 lines");
    }

    #endregion

    #region Overwrite Existing File

    [Fact]
    public async Task ExecuteAsync_ExistingFile_ShouldOverwrite()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "existing.txt");
        File.WriteAllText(filePath, "old content");
        var input = JsonSerializer.Serialize(new { path = filePath, content = "new content" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Overwritten");
        File.ReadAllText(filePath).Should().Be("new content");
    }

    #endregion

    #region Auto Create Directory

    [Fact]
    public async Task ExecuteAsync_NestedDir_ShouldAutoCreate()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "a", "b", "c", "deep.txt");
        var input = JsonSerializer.Serialize(new { path = filePath, content = "deep content" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Should().Be("deep content");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_EmptyPath_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { path = "", content = "test" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ShouldReturnError()
    {
        // Act
        var result = await _tool.ExecuteAsync("not json");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid input JSON");
    }

    #endregion
}
