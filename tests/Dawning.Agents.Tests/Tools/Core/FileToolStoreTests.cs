using System.Text.Json;
using System.Text.Json.Serialization;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// FileToolStore tests
/// </summary>
public class FileToolStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileToolStore _store;

    public FileToolStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"file_store_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new FileToolStore(globalToolsBasePath: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region SaveToolAsync

    [Fact]
    public async Task SaveToolAsync_Global_ShouldCreateFile()
    {
        // Arrange
        var definition = CreateDefinition("my_tool");

        // Act
        await _store.SaveToolAsync(definition, ToolScope.Global);

        // Assert
        var filePath = Path.Combine(_tempDir, ".dawning", "tools", "my_tool.tool.json");
        File.Exists(filePath).Should().BeTrue();

        var json = File.ReadAllText(filePath);
        var loaded = JsonSerializer.Deserialize<EphemeralToolDefinition>(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            }
        );
        loaded!.Name.Should().Be("my_tool");
        loaded.Description.Should().Be("A test tool");
        loaded.Script.Should().Be("echo test");
    }

    [Fact]
    public async Task SaveToolAsync_Session_ShouldThrow()
    {
        // Act
        var act = () => _store.SaveToolAsync(CreateDefinition("tool"), ToolScope.Session);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Session*");
    }

    #endregion

    #region LoadToolsAsync

    [Fact]
    public async Task LoadToolsAsync_NoDirectory_ShouldReturnEmpty()
    {
        // Act
        var tools = await _store.LoadToolsAsync(ToolScope.Global);

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadToolsAsync_WithSavedTools_ShouldLoadThem()
    {
        // Arrange
        await _store.SaveToolAsync(CreateDefinition("tool1"), ToolScope.Global);
        await _store.SaveToolAsync(CreateDefinition("tool2"), ToolScope.Global);

        // Act
        var tools = await _store.LoadToolsAsync(ToolScope.Global);

        // Assert
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain("tool1").And.Contain("tool2");
    }

    [Fact]
    public async Task LoadToolsAsync_ShouldSetCorrectScope()
    {
        // Arrange
        await _store.SaveToolAsync(CreateDefinition("s_tool"), ToolScope.Global);

        // Act
        var tools = await _store.LoadToolsAsync(ToolScope.Global);

        // Assert
        tools.Should().HaveCount(1);
        tools[0].Scope.Should().Be(ToolScope.Global);
    }

    [Fact]
    public async Task LoadToolsAsync_Session_ShouldThrow()
    {
        // Act
        var act = () => _store.LoadToolsAsync(ToolScope.Session);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region DeleteToolAsync

    [Fact]
    public async Task DeleteToolAsync_ExistingTool_ShouldRemoveFile()
    {
        // Arrange
        await _store.SaveToolAsync(CreateDefinition("to_delete"), ToolScope.Global);
        (await _store.ExistsAsync("to_delete", ToolScope.Global)).Should().BeTrue();

        // Act
        await _store.DeleteToolAsync("to_delete", ToolScope.Global);

        // Assert
        (await _store.ExistsAsync("to_delete", ToolScope.Global))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task DeleteToolAsync_NonexistentTool_ShouldNotThrow()
    {
        // Act
        var act = () => _store.DeleteToolAsync("nonexistent", ToolScope.Global);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteToolAsync_Session_ShouldThrow()
    {
        // Act
        var act = () => _store.DeleteToolAsync("tool", ToolScope.Session);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ExistsAsync

    [Fact]
    public async Task ExistsAsync_ExistingTool_ShouldReturnTrue()
    {
        // Arrange
        await _store.SaveToolAsync(CreateDefinition("exists"), ToolScope.Global);

        // Act
        var exists = await _store.ExistsAsync("exists", ToolScope.Global);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonexistentTool_ShouldReturnFalse()
    {
        // Act
        var exists = await _store.ExistsAsync("nonexistent", ToolScope.Global);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_Session_ShouldThrow()
    {
        // Act
        var act = () => _store.ExistsAsync("tool", ToolScope.Session);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Roundtrip

    [Fact]
    public async Task SaveAndLoad_ShouldPreserveAllProperties()
    {
        // Arrange
        var definition = new EphemeralToolDefinition
        {
            Name = "complex_tool",
            Description = "A complex tool with parameters",
            Script = "echo $name $count",
            Runtime = ScriptRuntime.Bash,
            Parameters =
            [
                new ScriptParameter
                {
                    Name = "name",
                    Description = "User name",
                    Type = "string",
                    Required = true,
                },
                new ScriptParameter
                {
                    Name = "count",
                    Description = "Repeat count",
                    Type = "int",
                    Required = false,
                    DefaultValue = "1",
                },
            ],
        };

        // Act
        await _store.SaveToolAsync(definition, ToolScope.Global);
        var loaded = (await _store.LoadToolsAsync(ToolScope.Global)).Single(t =>
            t.Name == "complex_tool"
        );

        // Assert
        loaded.Name.Should().Be("complex_tool");
        loaded.Description.Should().Be("A complex tool with parameters");
        loaded.Script.Should().Be("echo $name $count");
        loaded.Parameters.Should().HaveCount(2);
        loaded.Parameters[0].Name.Should().Be("name");
        loaded.Parameters[0].Required.Should().BeTrue();
        loaded.Parameters[1].Name.Should().Be("count");
        loaded.Parameters[1].DefaultValue.Should().Be("1");
    }

    #endregion

    private static EphemeralToolDefinition CreateDefinition(
        string name,
        string description = "A test tool"
    )
    {
        return new EphemeralToolDefinition
        {
            Name = name,
            Description = description,
            Script = "echo test",
        };
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("foo/../../../bar")]
    public async Task SaveToolAsync_PathTraversal_ShouldThrow(string maliciousName)
    {
        var definition = CreateDefinition(maliciousName);

        var act = () => _store.SaveToolAsync(definition, ToolScope.Global);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*path traversal*");
    }
}
