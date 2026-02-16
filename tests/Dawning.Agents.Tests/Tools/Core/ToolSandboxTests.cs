using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools.Core;

/// <summary>
/// ToolSandbox 集成测试（实际执行进程）
/// </summary>
public class ToolSandboxTests : IDisposable
{
    private readonly ToolSandbox _sandbox;
    private readonly string _tempDir;

    public ToolSandboxTests()
    {
        _sandbox = new ToolSandbox();
        _tempDir = Path.Combine(Path.GetTempPath(), $"sandbox_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SimpleCommand_ShouldReturnOutput()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo hello",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result
            .IsSuccess.Should()
            .BeTrue(
                "stdout={0}, stderr={1}, exitCode={2}",
                result.Stdout,
                result.Stderr,
                result.ExitCode
            );
        result.Stdout.Trim().Should().Be("hello");
        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FailingCommand_ShouldReturnNonZeroExit()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "exit 42",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_Stderr_ShouldCapture()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo error >&2",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result.Stderr.Trim().Should().Contain("error");
    }

    [Fact]
    public async Task ExecuteAsync_WorkingDirectory_ShouldRespect()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "marker.txt"), "found");

        // Act
        var result = await _sandbox.ExecuteAsync(
            "cat marker.txt",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result
            .IsSuccess.Should()
            .BeTrue(
                "stdout={0}, stderr={1}, exitCode={2}",
                result.Stdout,
                result.Stderr,
                result.ExitCode
            );
        result.Stdout.Trim().Should().Be("found");
    }

    [Fact]
    public async Task ExecuteAsync_NonexistentWorkingDir_ShouldReturnError()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo test",
            new ToolSandboxOptions { WorkingDirectory = "/nonexistent/dir" }
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Stderr.Should().Contain("Working directory does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ShouldKillProcess()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "sleep 60",
            new ToolSandboxOptions
            {
                WorkingDirectory = _tempDir,
                Timeout = TimeSpan.FromMilliseconds(500),
            }
        );

        // Assert
        result.TimedOut.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
    }

    [Fact]
    public async Task ExecuteAsync_EnvironmentVariables_ShouldBeAvailable()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo $MY_TEST_VAR",
            new ToolSandboxOptions
            {
                WorkingDirectory = _tempDir,
                Environment = new Dictionary<string, string> { ["MY_TEST_VAR"] = "custom_value" },
            }
        );

        // Assert
        result
            .IsSuccess.Should()
            .BeTrue(
                "stdout={0}, stderr={1}, exitCode={2}",
                result.Stdout,
                result.Stderr,
                result.ExitCode
            );
        result.Stdout.Trim().Should().Be("custom_value");
    }

    [Fact]
    public async Task ExecuteAsync_Duration_ShouldBeTracked()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo fast",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationToken_ShouldThrow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sandbox.ExecuteAsync(
                "sleep 10",
                new ToolSandboxOptions { WorkingDirectory = _tempDir },
                cts.Token
            )
        );
    }

    [Fact]
    public async Task ExecuteAsync_MultiLineCommand_ShouldWork()
    {
        // Act
        var result = await _sandbox.ExecuteAsync(
            "echo line1 && echo line2",
            new ToolSandboxOptions { WorkingDirectory = _tempDir }
        );

        // Assert
        result
            .IsSuccess.Should()
            .BeTrue(
                "stdout={0}, stderr={1}, exitCode={2}",
                result.Stdout,
                result.Stderr,
                result.ExitCode
            );
        result.Stdout.Should().Contain("line1");
        result.Stdout.Should().Contain("line2");
    }
}
