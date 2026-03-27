using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// CreateToolTool tests
/// </summary>
public class CreateToolToolTests
{
    private readonly Mock<IToolSandbox> _mockSandbox;
    private readonly Mock<IToolStore> _mockStore;
    private readonly ToolSession _session;
    private readonly CreateToolTool _tool;

    public CreateToolToolTests()
    {
        _mockSandbox = new Mock<IToolSandbox>();
        _mockStore = new Mock<IToolStore>();
        _session = new ToolSession(_mockSandbox.Object, _mockStore.Object);
        _tool = new CreateToolTool(_session);
    }

    #region Tool Properties

    [Fact]
    public void Properties_ShouldBeCorrect()
    {
        _tool.Name.Should().Be("create_tool");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.High);
        _tool.RequiresConfirmation.Should().BeFalse();
        _tool.Category.Should().Be("Core");
    }

    #endregion

    #region Successful Creation

    [Fact]
    public async Task ExecuteAsync_SessionScope_ShouldCreateTool()
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                name = "count_lines",
                description = "Count lines in a file",
                script = "wc -l $file",
                scope = "session",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("count_lines");
        result.Output.Should().Contain("session");
        _session.GetSessionTools().Should().HaveCount(1);
        _session.GetSessionTools()[0].Name.Should().Be("count_lines");
    }

    [Fact]
    public async Task ExecuteAsync_WithParameters_ShouldCreateTool()
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                name = "greet",
                description = "Greet someone",
                script = "echo Hello $name",
                parameters = new[]
                {
                    new
                    {
                        name = "name",
                        description = "Name to greet",
                        type = "string",
                        required = true,
                    },
                },
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("name");
        _session.GetSessionTools().Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultScope_ShouldBeSession()
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                name = "my_tool",
                description = "A tool",
                script = "echo hi",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("session");
    }

    [Fact]
    public async Task ExecuteAsync_UserScope_ShouldPromote()
    {
        // Arrange
        _mockStore
            .Setup(s =>
                s.SaveToolAsync(
                    It.IsAny<EphemeralToolDefinition>(),
                    ToolScope.User,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var input = JsonSerializer.Serialize(
            new
            {
                name = "promoted",
                description = "Promoted tool",
                script = "echo promoted",
                scope = "user",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("user");
        _mockStore.Verify(
            s =>
                s.SaveToolAsync(
                    It.Is<EphemeralToolDefinition>(d => d.Name == "promoted"),
                    ToolScope.User,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_GlobalScope_ShouldPromote()
    {
        // Arrange
        _mockStore
            .Setup(s =>
                s.SaveToolAsync(
                    It.IsAny<EphemeralToolDefinition>(),
                    ToolScope.Global,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var input = JsonSerializer.Serialize(
            new
            {
                name = "global_tool",
                description = "Global tool",
                script = "echo global",
                scope = "global",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        _mockStore.Verify(
            s =>
                s.SaveToolAsync(
                    It.IsAny<EphemeralToolDefinition>(),
                    ToolScope.Global,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    #endregion

    #region Validation - Tool Name

    [Theory]
    [InlineData("count_lines")]
    [InlineData("a")]
    [InlineData("tool123")]
    [InlineData("my_long_tool_name")]
    public async Task ExecuteAsync_ValidName_ShouldSucceed(string name)
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                name,
                description = "test",
                script = "echo test",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("CamelCase")]
    [InlineData("has spaces")]
    [InlineData("has-dashes")]
    [InlineData("_leading_underscore")]
    [InlineData("trailing_")]
    [InlineData("has.dots")]
    [InlineData("UPPERCASE")]
    public async Task ExecuteAsync_InvalidName_ShouldFail(string name)
    {
        // Arrange
        var input = JsonSerializer.Serialize(
            new
            {
                name,
                description = "test",
                script = "echo test",
            }
        );

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("snake_case");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ShouldReturnError()
    {
        // Act
        var result = await _tool.ExecuteAsync("not valid json");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid tool definition");
    }

    [Fact]
    public async Task ExecuteAsync_MissingName_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { description = "test", script = "echo test" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MissingScript_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { name = "my_tool", description = "test" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullSession_Throws()
    {
        var act = () => new CreateToolTool(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("session");
    }

    #endregion
}
