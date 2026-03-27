using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// EphemeralTool tests
/// </summary>
public class EphemeralToolTests
{
    private readonly Mock<IToolSandbox> _mockSandbox;

    public EphemeralToolTests()
    {
        _mockSandbox = new Mock<IToolSandbox>();
    }

    #region Tool Properties

    [Fact]
    public void Properties_ShouldReflectDefinition()
    {
        // Arrange
        var definition = CreateDefinition("my_tool", "A test tool");

        // Act
        var tool = CreateTool(definition);

        // Assert
        tool.Name.Should().Be("my_tool");
        tool.Description.Should().Be("A test tool");
        tool.RiskLevel.Should().Be(ToolRiskLevel.High);
        tool.RequiresConfirmation.Should().BeTrue();
        tool.Category.Should().Be("Ephemeral");
        tool.Definition.Should().BeSameAs(definition);
    }

    [Fact]
    public void ParametersSchema_NoParams_ShouldReturnEmptySchema()
    {
        // Arrange
        var definition = CreateDefinition("no_params", "No parameters");

        // Act
        var tool = CreateTool(definition);
        var schema = JsonDocument.Parse(tool.ParametersSchema);

        // Assert
        schema.RootElement.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void ParametersSchema_WithParams_ShouldIncludeThem()
    {
        // Arrange
        var definition = CreateDefinition("with_params", "Has parameters");
        definition.Parameters.Add(
            new ScriptParameter
            {
                Name = "filename",
                Description = "The file name",
                Type = "string",
                Required = true,
            }
        );

        // Act
        var tool = CreateTool(definition);
        var schema = JsonDocument.Parse(tool.ParametersSchema);

        // Assert
        var props = schema.RootElement.GetProperty("properties");
        props.TryGetProperty("filename", out _).Should().BeTrue();

        var required = schema.RootElement.GetProperty("required");
        required.EnumerateArray().Any(e => e.GetString() == "filename").Should().BeTrue();
    }

    #endregion

    #region Execution

    [Fact]
    public async Task ExecuteAsync_ShouldSubstituteParameters()
    {
        // Arrange
        var definition = CreateDefinition("greet", "Greet someone", "echo Hello $name");
        definition.Parameters.Add(
            new ScriptParameter { Name = "name", Description = "Name to greet" }
        );

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    "echo Hello 'Alice'",
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "Hello Alice\n" });

        var tool = CreateTool(definition);
        var input = JsonSerializer.Serialize(new { name = "Alice" });

        // Act
        var result = await tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Hello Alice");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassEnvironmentVariables()
    {
        // Arrange
        var definition = CreateDefinition("env_tool", "Uses env vars", "echo $TOOL_PARAM_NAME");
        definition.Parameters.Add(new ScriptParameter { Name = "name", Description = "A name" });

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.Is<ToolSandboxOptions>(o =>
                        o.Environment.ContainsKey("TOOL_PARAM_NAME")
                        && o.Environment["TOOL_PARAM_NAME"] == "Bob"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "Bob\n" });

        var tool = CreateTool(definition);
        var input = JsonSerializer.Serialize(new { name = "Bob" });

        // Act
        await tool.ExecuteAsync(input);

        // Assert
        _mockSandbox.Verify(
            s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.Is<ToolSandboxOptions>(o => o.Environment["TOOL_PARAM_NAME"] == "Bob"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParam_ShouldReturnError()
    {
        // Arrange
        var definition = CreateDefinition("needs_param", "Needs a param", "echo $name");
        definition.Parameters.Add(
            new ScriptParameter
            {
                Name = "name",
                Description = "Required name",
                Required = true,
            }
        );

        var tool = CreateTool(definition);
        var input = JsonSerializer.Serialize(new { other = "value" });

        // Act
        var result = await tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("name");
        result.Error.Should().Contain("missing");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultValue_ShouldBeUsed()
    {
        // Arrange
        var definition = CreateDefinition("with_default", "Has default", "echo $greeting");
        definition.Parameters.Add(
            new ScriptParameter
            {
                Name = "greeting",
                Description = "Greeting text",
                Required = true,
                DefaultValue = "hi",
            }
        );

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    "echo 'hi'",
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "hi\n" });

        var tool = CreateTool(definition);
        var input = "{}";

        // Act
        var result = await tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ShouldReturnError()
    {
        // Arrange
        var definition = CreateDefinition("slow_tool", "Slow", "sleep 100");

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ToolExecutionResult
                {
                    ExitCode = -1,
                    TimedOut = true,
                    Duration = TimeSpan.FromSeconds(30),
                }
            );

        var tool = CreateTool(definition);

        // Act
        var result = await tool.ExecuteAsync("{}");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExit_ShouldReturnError()
    {
        // Arrange
        var definition = CreateDefinition("fail_tool", "Fails", "exit 1");

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 1, Stderr = "error message" });

        var tool = CreateTool(definition);

        // Act
        var result = await tool.ExecuteAsync("{}");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Exit code 1");
        result.Error.Should().Contain("error message");
    }

    [Fact]
    public async Task ExecuteAsync_SingleParam_PlainTextInput_ShouldWork()
    {
        // Arrange
        var definition = CreateDefinition("single", "Single param", "echo $query");
        definition.Parameters.Add(
            new ScriptParameter { Name = "query", Description = "Search query" }
        );

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "result\n" });

        var tool = CreateTool(definition);

        // Act — pass plain text instead of JSON for single-parameter tool
        var result = await tool.ExecuteAsync("my search query");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoOutput_ShouldReturnPlaceholder()
    {
        // Arrange
        var definition = CreateDefinition("silent", "Silent tool", "true");

        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "" });

        var tool = CreateTool(definition);

        // Act
        var result = await tool.ExecuteAsync("{}");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("(no output)");
    }

    #endregion

    #region Helpers

    private EphemeralTool CreateTool(EphemeralToolDefinition definition)
    {
        return new EphemeralTool(definition, _mockSandbox.Object);
    }

    private static EphemeralToolDefinition CreateDefinition(
        string name,
        string description,
        string script = "echo test"
    )
    {
        return new EphemeralToolDefinition
        {
            Name = name,
            Description = description,
            Script = script,
        };
    }

    #endregion
}
