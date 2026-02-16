using System.Diagnostics;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 工具沙箱实现 — 通过 Process 执行 shell 命令，支持超时和工作目录限制
/// </summary>
public sealed class ToolSandbox : IToolSandbox
{
    private readonly ILogger<ToolSandbox> _logger;

    /// <summary>
    /// 创建工具沙箱
    /// </summary>
    public ToolSandbox(ILogger<ToolSandbox>? logger = null)
    {
        _logger = logger ?? NullLogger<ToolSandbox>.Instance;
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(
        string command,
        ToolSandboxOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ToolSandboxOptions();

        var workingDir = Path.GetFullPath(options.WorkingDirectory);
        if (!Directory.Exists(workingDir))
        {
            return new ToolExecutionResult
            {
                ExitCode = 1,
                Stderr = $"Working directory does not exist: {workingDir}",
            };
        }

        _logger.LogDebug(
            "Executing command in {WorkingDir} (timeout={Timeout}s): {Command}",
            workingDir,
            options.Timeout.TotalSeconds,
            TruncateForLog(command)
        );

        var shell = GetShell();
        var psi = new ProcessStartInfo
        {
            FileName = shell.FileName,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Use ArgumentList for correct argument passing on all platforms
        foreach (var arg in shell.BuildArguments(command))
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var env in options.Environment)
        {
            psi.Environment[env.Key] = env.Value;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(options.Timeout);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                sw.Stop();

                _logger.LogDebug(
                    "Command completed: exitCode={ExitCode}, duration={Duration}ms",
                    process.ExitCode,
                    sw.ElapsedMilliseconds
                );

                return new ToolExecutionResult
                {
                    ExitCode = process.ExitCode,
                    Stdout = stdout,
                    Stderr = stderr,
                    Duration = sw.Elapsed,
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout (not user cancellation)
                sw.Stop();
                TryKillProcess(process);

                _logger.LogWarning(
                    "Command timed out after {Timeout}s: {Command}",
                    options.Timeout.TotalSeconds,
                    TruncateForLog(command)
                );

                return new ToolExecutionResult
                {
                    ExitCode = -1,
                    Stderr = $"Command timed out after {options.Timeout.TotalSeconds}s",
                    Duration = sw.Elapsed,
                    TimedOut = true,
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to execute command: {Command}", TruncateForLog(command));

            return new ToolExecutionResult
            {
                ExitCode = -1,
                Stderr = $"Failed to execute command: {ex.Message}",
                Duration = sw.Elapsed,
            };
        }
    }

    private static ShellInfo GetShell()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ShellInfo("cmd.exe", args => ["/c", args]);
        }

        // macOS / Linux
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        return new ShellInfo(shell, args => ["-c", args]);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static string TruncateForLog(string text, int maxLength = 200)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private record ShellInfo(string FileName, Func<string, string[]> BuildArguments);
}
