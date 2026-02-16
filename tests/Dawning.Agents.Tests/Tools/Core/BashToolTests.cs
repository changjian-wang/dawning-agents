using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// BashTool 测试（使用模拟 IToolSandbox）
/// </summary>
public class BashToolTests
{
    private readonly Mock<IToolSandbox> _mockSandbox;
    private readonly BashTool _tool;

    public BashToolTests()
    {
        _mockSandbox = new Mock<IToolSandbox>();
        _tool = new BashTool(_mockSandbox.Object);
    }

    #region Tool Properties

    [Fact]
    public void Properties_ShouldBeCorrect()
    {
        _tool.Name.Should().Be("bash");
        _tool.RiskLevel.Should().Be(ToolRiskLevel.High);
        _tool.RequiresConfirmation.Should().BeTrue();
        _tool.Category.Should().Be("Core");
    }

    [Fact]
    public void ParametersSchema_ShouldBeValidJson()
    {
        var act = () => JsonDocument.Parse(_tool.ParametersSchema);
        act.Should().NotThrow();
    }

    #endregion

    #region Successful Execution

    [Fact]
    public async Task ExecuteAsync_ShouldExecuteCommand()
    {
        // Arrange
        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    "echo hello",
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "hello\n" });

        var input = JsonSerializer.Serialize(new { command = "echo hello" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("hello");
    }

    [Fact]
    public async Task ExecuteAsync_PlainStringInput_ShouldTreatAsCommand()
    {
        // Arrange
        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync("ls", It.IsAny<ToolSandboxOptions>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "file.txt\n" });

        // Act
        var result = await _tool.ExecuteAsync("ls");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("file.txt");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ShouldPassToSandbox()
    {
        // Arrange
        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    "sleep 1",
                    It.Is<ToolSandboxOptions>(o => o.Timeout == TimeSpan.FromSeconds(60)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "" });

        var input = JsonSerializer.Serialize(new { command = "sleep 1", timeout = 60 });

        // Act
        await _tool.ExecuteAsync(input);

        // Assert
        _mockSandbox.Verify(
            s =>
                s.ExecuteAsync(
                    "sleep 1",
                    It.Is<ToolSandboxOptions>(o => o.Timeout == TimeSpan.FromSeconds(60)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithStderr_ShouldIncludeInOutput()
    {
        // Arrange
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
                    ExitCode = 0,
                    Stdout = "output",
                    Stderr = "warning",
                }
            );

        var input = JsonSerializer.Serialize(new { command = "test" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("output");
        result.Output.Should().Contain("[stderr]");
        result.Output.Should().Contain("warning");
    }

    [Fact]
    public async Task ExecuteAsync_NoOutput_ShouldShowPlaceholder()
    {
        // Arrange
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
                    ExitCode = 0,
                    Stdout = "",
                    Stderr = "",
                }
            );

        var input = JsonSerializer.Serialize(new { command = "true" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("(no output)");
    }

    #endregion

    #region Failed Execution

    [Fact]
    public async Task ExecuteAsync_NonZeroExit_ShouldReturnFail()
    {
        // Arrange
        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 1, Stderr = "file not found" });

        var input = JsonSerializer.Serialize(new { command = "cat missing.txt" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Exit code 1");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ShouldReturnFail()
    {
        // Arrange
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

        var input = JsonSerializer.Serialize(new { command = "sleep 100" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
    }

    #endregion

    #region Dangerous Commands

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf /*")]
    [InlineData("rm -rf ~")]
    [InlineData(":(){:|:&};:")]
    [InlineData("mkfs.ext4 /dev/sda")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("shutdown -h now")]
    [InlineData("chmod -R 777 /")]
    public async Task ExecuteAsync_DangerousCommand_ShouldBlock(string command)
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { command });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Blocked dangerous command");

        // Verify sandbox was never called
        _mockSandbox.Verify(
            s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Theory]
    [InlineData("rm -rf ./build")]
    [InlineData("echo hello")]
    [InlineData("ls -la")]
    [InlineData("git status")]
    public async Task ExecuteAsync_SafeCommand_ShouldNotBlock(string command)
    {
        // Arrange
        _mockSandbox
            .Setup(s =>
                s.ExecuteAsync(
                    It.IsAny<string>(),
                    It.IsAny<ToolSandboxOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ToolExecutionResult { ExitCode = 0, Stdout = "ok" });

        var input = JsonSerializer.Serialize(new { command });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ShouldReturnError()
    {
        // Arrange
        var input = JsonSerializer.Serialize(new { command = "" });

        // Act
        var result = await _tool.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    #endregion
}
