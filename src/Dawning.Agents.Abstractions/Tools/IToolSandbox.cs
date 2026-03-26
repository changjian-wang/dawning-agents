namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool execution sandbox — controls the execution environment and security policy for bash/script commands.
/// </summary>
public interface IToolSandbox
{
    /// <summary>
    /// Executes a command in the sandbox.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="options">Sandbox options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    Task<ToolExecutionResult> ExecuteAsync(
        string command,
        ToolSandboxOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Sandbox execution options.
/// </summary>
public class ToolSandboxOptions : IValidatableOptions
{
    /// <summary>
    /// Working directory.
    /// </summary>
    public string WorkingDirectory { get; set; } = ".";

    /// <summary>
    /// Maximum execution time.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sandbox mode.
    /// </summary>
    public SandboxMode Mode { get; set; } = SandboxMode.Trust;

    /// <summary>
    /// Script runtime type.
    /// </summary>
    public ScriptRuntime Runtime { get; set; } = ScriptRuntime.Bash;

    /// <summary>
    /// Environment variables.
    /// </summary>
    public IDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

    /// <inheritdoc />
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            throw new InvalidOperationException("ToolSandboxOptions.WorkingDirectory is required.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("ToolSandboxOptions.Timeout must be positive.");
        }
    }
}

/// <summary>
/// Sandbox mode.
/// </summary>
public enum SandboxMode
{
    /// <summary>
    /// Trust mode — executes directly on the host (development environment).
    /// </summary>
    Trust,

    /// <summary>
    /// Working directory restriction — limits execution to the specified directory.
    /// </summary>
    WorkingDir,

    /// <summary>
    /// Timeout mode — only limits execution time.
    /// </summary>
    Timeout,
}

/// <summary>
/// Tool execution result.
/// </summary>
public record ToolExecutionResult
{
    /// <summary>
    /// Process exit code.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output.
    /// </summary>
    public string Stdout { get; init; } = "";

    /// <summary>
    /// Standard error.
    /// </summary>
    public string Stderr { get; init; } = "";

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Whether the execution timed out.
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// Whether the execution succeeded (exit code is 0 and did not time out).
    /// </summary>
    public bool IsSuccess => ExitCode == 0 && !TimedOut;
}
