using System.Diagnostics;
using System.Text;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 进程/终端工具 - 提供命令执行能力
/// </summary>
/// <remarks>
/// <para>所有命令执行都是高风险操作，需要用户确认</para>
/// <para>支持设置工作目录、超时时间等</para>
/// </remarks>
public class ProcessTool
{
    private readonly ProcessToolOptions _options;

    /// <summary>
    /// 创建进程工具
    /// </summary>
    /// <param name="options">工具配置选项</param>
    public ProcessTool(ProcessToolOptions? options = null)
    {
        _options = options ?? new ProcessToolOptions();
    }

    /// <summary>
    /// 执行终端命令（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "在终端中执行命令。这是高风险操作，可能会修改系统状态，需要用户确认。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Process"
    )]
    public async Task<ToolResult> RunCommand(
        [ToolParameter("要执行的命令")] string command,
        [ToolParameter("工作目录（可选）")] string? workingDirectory = null,
        [ToolParameter("超时时间（秒，默认 60）")] int timeoutSeconds = 60,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return ToolResult.Fail("命令不能为空");
            }

            // 检查是否在允许的目录中执行
            if (
                _options.AllowedDirectories.Count > 0
                && !string.IsNullOrWhiteSpace(workingDirectory)
            )
            {
                var normalizedDir = Path.GetFullPath(workingDirectory);
                if (
                    !_options.AllowedDirectories.Any(d =>
                        normalizedDir.StartsWith(
                            Path.GetFullPath(d),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    return ToolResult.Fail($"不允许在此目录执行命令: {workingDirectory}");
                }
            }

            // 检查是否包含禁止的命令
            if (
                _options.BlockedCommands.Any(blocked =>
                    command.Contains(blocked, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return ToolResult.Fail("命令包含被禁止的操作");
            }

            var (shell, shellArgs) = GetShellCommand(command);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略终止进程失败
                }

                return ToolResult.Fail($"命令执行超时（{timeoutSeconds} 秒）");
            }

            var result = new StringBuilder();
            result.AppendLine($"退出代码: {process.ExitCode}");

            if (outputBuilder.Length > 0)
            {
                result.AppendLine();
                result.AppendLine("--- 标准输出 ---");
                result.Append(TruncateOutput(outputBuilder.ToString()));
            }

            if (errorBuilder.Length > 0)
            {
                result.AppendLine();
                result.AppendLine("--- 标准错误 ---");
                result.Append(TruncateOutput(errorBuilder.ToString()));
            }

            return process.ExitCode == 0
                ? ToolResult.Ok(result.ToString().TrimEnd())
                : ToolResult.Fail(result.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"执行命令失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 启动后台进程（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "启动后台进程。进程将在后台运行，不会等待其完成。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Process"
    )]
    public Task<ToolResult> StartBackgroundProcess(
        [ToolParameter("要执行的命令")] string command,
        [ToolParameter("工作目录（可选）")] string? workingDirectory = null
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return Task.FromResult(ToolResult.Fail("命令不能为空"));
            }

            var (shell, shellArgs) = GetShellCommand(command);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            return Task.FromResult(
                ToolResult.Ok($"后台进程已启动\nPID: {process.Id}\n命令: {command}")
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"启动进程失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 终止进程（高风险，需要确认）
    /// </summary>
    [FunctionTool(
        "终止指定的进程。",
        RequiresConfirmation = true,
        RiskLevel = ToolRiskLevel.High,
        Category = "Process"
    )]
    public Task<ToolResult> KillProcess(
        [ToolParameter("进程 ID")] int processId,
        [ToolParameter("是否终止整个进程树")] bool entireProcessTree = false
    )
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree);
            return Task.FromResult(ToolResult.Ok($"进程 {processId} 已终止"));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(ToolResult.Fail($"进程 {processId} 不存在"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"终止进程失败: {ex.Message}"));
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// 列出运行中的进程
    /// </summary>
    [FunctionTool("列出当前运行的进程。", Category = "Process")]
    public Task<ToolResult> ListProcesses(
        [ToolParameter("进程名称过滤（可选）")] string? nameFilter = null
    )
    {
        try
        {
            var processes = Process.GetProcesses();

            try
            {
                IEnumerable<Process> filtered = processes;

                if (!string.IsNullOrWhiteSpace(nameFilter))
                {
                    filtered = processes.Where(p =>
                        p.ProcessName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)
                    );
                }

                var result = new StringBuilder();
                result.AppendLine($"找到 {processes.Length} 个进程:");
                result.AppendLine();
                result.AppendLine("PID\t内存(MB)\t进程名");
                result.AppendLine(new string('-', 50));

                var count = 0;
                foreach (var p in filtered.OrderBy(p => p.ProcessName))
                {
                    if (count >= 100)
                    {
                        break;
                    }

                    try
                    {
                        var memoryMb = p.WorkingSet64 / 1024.0 / 1024.0;
                        result.AppendLine($"{p.Id}\t{memoryMb:F1}\t\t{p.ProcessName}");
                        count++;
                    }
                    catch
                    {
                        // 某些系统进程无法访问
                    }
                }

                if (processes.Length > 100)
                {
                    result.AppendLine($"... 还有 {processes.Length - 100} 个进程未显示");
                }

                return Task.FromResult(ToolResult.Ok(result.ToString().TrimEnd()));
            }
            finally
            {
                // 必须释放所有 Process 对象，否则会内存泄漏
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"列出进程失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 获取进程详细信息
    /// </summary>
    [FunctionTool("获取指定进程的详细信息。", Category = "Process")]
    public Task<ToolResult> GetProcessInfo([ToolParameter("进程 ID")] int processId)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);

            var result = new StringBuilder();
            result.AppendLine($"进程名: {process.ProcessName}");
            result.AppendLine($"PID: {process.Id}");
            result.AppendLine($"启动时间: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
            result.AppendLine($"运行时间: {DateTime.Now - process.StartTime}");
            result.AppendLine($"工作内存: {process.WorkingSet64 / 1024.0 / 1024.0:F2} MB");
            result.AppendLine($"虚拟内存: {process.VirtualMemorySize64 / 1024.0 / 1024.0:F2} MB");
            result.AppendLine($"线程数: {process.Threads.Count}");
            result.AppendLine($"句柄数: {process.HandleCount}");
            result.AppendLine($"优先级: {process.PriorityClass}");
            result.AppendLine($"响应中: {process.Responding}");

            try
            {
                result.AppendLine($"主模块: {process.MainModule?.FileName}");
            }
            catch
            {
                // 某些进程无法访问主模块
            }

            return Task.FromResult(ToolResult.Ok(result.ToString().TrimEnd()));
        }
        catch (ArgumentException)
        {
            return Task.FromResult(ToolResult.Fail($"进程 {processId} 不存在"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"获取进程信息失败: {ex.Message}"));
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// 获取环境变量
    /// </summary>
    [FunctionTool("获取系统环境变量。", Category = "Process")]
    public Task<ToolResult> GetEnvironmentVariable(
        [ToolParameter("环境变量名称（为空则列出所有）")] string? name = null
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var envVars = Environment.GetEnvironmentVariables();
                var result = new StringBuilder();
                result.AppendLine($"共 {envVars.Count} 个环境变量:");
                result.AppendLine();

                foreach (var key in envVars.Keys.Cast<string>().OrderBy(k => k))
                {
                    var value = envVars[key]?.ToString();
                    if (value != null && value.Length > 100)
                    {
                        value = value[..100] + "...";
                    }
                    result.AppendLine($"{key}={value}");
                }

                return Task.FromResult(ToolResult.Ok(result.ToString().TrimEnd()));
            }
            else
            {
                var value = Environment.GetEnvironmentVariable(name);
                return Task.FromResult(
                    value != null
                        ? ToolResult.Ok($"{name}={value}")
                        : ToolResult.Fail($"环境变量 {name} 不存在")
                );
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"获取环境变量失败: {ex.Message}"));
        }
    }

    private static (string shell, string args) GetShellCommand(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return ("cmd.exe", $"/c {command}");
        }
        else
        {
            return ("/bin/bash", $"-c \"{command.Replace("\"", "\\\"")}\"");
        }
    }

    private static string TruncateOutput(string output, int maxLength = 10000)
    {
        if (output.Length <= maxLength)
        {
            return output;
        }

        return output[..maxLength] + $"\n\n... (截断，总长度 {output.Length} 字符)";
    }
}

/// <summary>
/// 进程工具配置选项
/// </summary>
public class ProcessToolOptions
{
    /// <summary>
    /// 允许执行命令的目录列表（为空则不限制）
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = [];

    /// <summary>
    /// 禁止的命令关键词列表
    /// </summary>
    public List<string> BlockedCommands { get; set; } =
    ["rm -rf /", "format c:", "del /s /q c:\\", ":(){:|:&};:"];

    /// <summary>
    /// 默认超时时间（秒）
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;
}
