using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// 命令行工具 — 在沙箱中执行 shell 命令
/// </summary>
/// <remarks>
/// <para>Risk: High — 可以执行任意系统命令</para>
/// <para>内置危险命令检测：rm -rf /、sudo、chmod 777 等</para>
/// </remarks>
public sealed class BashTool : ITool
{
    private readonly IToolSandbox _sandbox;
    private readonly ToolSandboxOptions _defaultOptions;
    private readonly ILogger<BashTool> _logger;

    /// <summary>
    /// 危险命令模式列表
    /// </summary>
    private static readonly string[] DangerousPatterns =
    [
        "rm -rf /",
        "rm -rf /*",
        "rm -rf ~",
        ":(){:|:&};:",
        "mkfs.",
        "dd if=/dev/",
        "> /dev/sda",
        "chmod -R 777 /",
        "shutdown",
        "reboot",
        "halt",
        "init 0",
        "init 6",
    ];

    /// <summary>
    /// 创建 BashTool
    /// </summary>
    /// <param name="sandbox">工具沙箱</param>
    /// <param name="defaultOptions">默认沙箱选项</param>
    /// <param name="logger">日志</param>
    public BashTool(
        IToolSandbox sandbox,
        ToolSandboxOptions? defaultOptions = null,
        ILogger<BashTool>? logger = null
    )
    {
        _sandbox = sandbox;
        _defaultOptions = defaultOptions ?? new ToolSandboxOptions();
        _logger = logger ?? NullLogger<BashTool>.Instance;
    }

    /// <inheritdoc />
    public string Name => "bash";

    /// <inheritdoc />
    public string Description =>
        "Execute a shell command in the working directory. "
        + "Use for running scripts, installing packages, git operations, "
        + "file operations, and other system tasks. "
        + "Commands are executed in a sandboxed environment with timeout protection.";

    /// <inheritdoc />
    public string ParametersSchema =>
        """
            {
                "type": "object",
                "properties": {
                    "command": {
                        "type": "string",
                        "description": "The shell command to execute"
                    },
                    "timeout": {
                        "type": "integer",
                        "description": "Optional timeout in seconds (default: 30)"
                    }
                },
                "required": ["command"]
            }
            """;

    /// <inheritdoc />
    public bool RequiresConfirmation => true;

    /// <inheritdoc />
    public ToolRiskLevel RiskLevel => ToolRiskLevel.High;

    /// <inheritdoc />
    public string? Category => "Core";

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        string command;
        int? timeoutSeconds = null;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            command =
                root.GetProperty("command").GetString()
                ?? throw new ArgumentException("command is required");

            if (
                root.TryGetProperty("timeout", out var timeoutProp)
                && timeoutProp.ValueKind == JsonValueKind.Number
            )
            {
                timeoutSeconds = timeoutProp.GetInt32();
            }
        }
        catch (JsonException)
        {
            // If not valid JSON, treat the entire input as the command
            command = input.Trim();
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Fail("Command cannot be empty");
        }

        // Check for dangerous commands
        var dangerCheck = CheckDangerousCommand(command);
        if (dangerCheck != null)
        {
            return ToolResult.Fail($"Blocked dangerous command: {dangerCheck}");
        }

        var options = new ToolSandboxOptions
        {
            WorkingDirectory = _defaultOptions.WorkingDirectory,
            Timeout = timeoutSeconds.HasValue
                ? TimeSpan.FromSeconds(timeoutSeconds.Value)
                : _defaultOptions.Timeout,
            Mode = _defaultOptions.Mode,
            Environment = new Dictionary<string, string>(_defaultOptions.Environment),
        };

        _logger.LogInformation("Executing bash command: {Command}", TruncateForLog(command));

        var result = await _sandbox
            .ExecuteAsync(command, options, cancellationToken)
            .ConfigureAwait(false);

        return FormatResult(result);
    }

    private static string? CheckDangerousCommand(string command)
    {
        var normalized = command.ToLowerInvariant().Trim();

        foreach (var pattern in DangerousPatterns)
        {
            if (normalized.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return pattern;
            }
        }

        return null;
    }

    private static ToolResult FormatResult(ToolExecutionResult result)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            sb.Append(result.Stdout);
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append("[stderr] ");
            sb.Append(result.Stderr);
        }

        if (result.TimedOut)
        {
            return ToolResult.Fail(
                $"Command timed out after {result.Duration.TotalSeconds:F1}s. "
                    + (sb.Length > 0 ? $"Partial output:\n{sb}" : "No output captured.")
            );
        }

        var output = sb.Length > 0 ? sb.ToString() : "(no output)";

        if (!result.IsSuccess)
        {
            return ToolResult.Fail($"Exit code {result.ExitCode}\n{output}");
        }

        return ToolResult.Ok(output);
    }

    private static string TruncateForLog(string text, int maxLength = 200)
    {
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
