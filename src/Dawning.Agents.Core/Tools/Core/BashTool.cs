using System.Globalization;
using System.Text;
using System.Text.Json;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Tools.Core;

/// <summary>
/// Command-line tool — executes shell commands in a sandbox.
/// </summary>
/// <remarks>
/// <para>Risk: High — can execute arbitrary system commands.</para>
/// <para>Built-in dangerous command detection: rm -rf /, sudo, chmod 777, etc.</para>
/// </remarks>
public sealed class BashTool : ITool
{
    private readonly IToolSandbox _sandbox;
    private readonly ToolSandboxOptions _defaultOptions;
    private readonly CommandAnalyzer _commandAnalyzer;
    private readonly ILogger<BashTool> _logger;

    /// <summary>
    /// Creates a <see cref="BashTool"/>.
    /// </summary>
    /// <param name="sandbox">The tool sandbox.</param>
    /// <param name="defaultOptions">Default sandbox options.</param>
    /// <param name="commandAnalyzer">Command analyzer (optional; uses default configuration).</param>
    /// <param name="logger">The logger.</param>
    public BashTool(
        IToolSandbox sandbox,
        ToolSandboxOptions? defaultOptions = null,
        CommandAnalyzer? commandAnalyzer = null,
        ILogger<BashTool>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        _sandbox = sandbox;
        _defaultOptions = defaultOptions ?? new ToolSandboxOptions();
        _commandAnalyzer = commandAnalyzer ?? new CommandAnalyzer();
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

            if (
                !root.TryGetProperty("command", out var commandProp)
                || string.IsNullOrWhiteSpace(commandProp.GetString())
            )
            {
                throw new ArgumentException("Command cannot be empty");
            }

            command = commandProp.GetString()!;

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
        catch (ArgumentException ex)
        {
            return ToolResult.Fail(ex.Message);
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Fail("Command cannot be empty");
        }

        // Analyze command safety
        var analysis = _commandAnalyzer.Analyze(command);
        if (!analysis.IsAllowed)
        {
            _logger.LogWarning(
                "Blocked command: {Command} — {Reason}",
                TruncateForLog(command),
                analysis.Message
            );
            return ToolResult.Fail($"Blocked dangerous command: {analysis.Message}");
        }

        if (analysis.HasWarning)
        {
            _logger.LogWarning(
                "Command warning: {Command} — {Warning}",
                TruncateForLog(command),
                analysis.Message
            );
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
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Command timed out after {result.Duration.TotalSeconds:F1}s. "
                ) + (sb.Length > 0 ? $"Partial output:\n{sb}" : "No output captured.")
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
