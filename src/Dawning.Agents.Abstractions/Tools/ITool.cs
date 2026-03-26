namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Tool execution result.
/// </summary>
public record ToolResult
{
    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Execution output content.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Error message (when <see cref="Success"/> is <see langword="false"/>).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether user confirmation is required (pre-check for high-risk operations).
    /// </summary>
    public bool RequiresConfirmation { get; init; } = false;

    /// <summary>
    /// Confirmation prompt message.
    /// </summary>
    public string? ConfirmationMessage { get; init; }

    /// <summary>
    /// Execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ToolResult Ok(string output) => new() { Success = true, Output = output };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static ToolResult Fail(string error) =>
        new()
        {
            Success = false,
            Output = string.Empty,
            Error = error,
        };

    /// <summary>
    /// Creates a result that requires confirmation.
    /// </summary>
    public static ToolResult NeedConfirmation(string message) =>
        new()
        {
            Success = false,
            Output = string.Empty,
            RequiresConfirmation = true,
            ConfirmationMessage = message,
        };
}

/// <summary>
/// Tool interface — external capabilities that an Agent can invoke.
/// </summary>
/// <remarks>
/// <para>Tools serve as the bridge between an Agent and the external world.</para>
/// <para>Each tool has a name, description, and parameter schema for the LLM to understand how to invoke it.</para>
/// </remarks>
public interface ITool
{
    /// <summary>
    /// Tool name (unique identifier).
    /// </summary>
    /// <example>Search, Calculate, GetWeather</example>
    string Name { get; }

    /// <summary>
    /// Tool description (for the LLM to understand the tool's purpose).
    /// </summary>
    /// <example>Search web content and return relevant results.</example>
    string Description { get; }

    /// <summary>
    /// JSON Schema of the parameters (for the LLM to understand parameter format).
    /// </summary>
    /// <example>{"type":"object","properties":{"query":{"type":"string"}}}</example>
    string ParametersSchema { get; }

    /// <summary>
    /// Whether user confirmation is required before execution.
    /// </summary>
    bool RequiresConfirmation { get; }

    /// <summary>
    /// Risk level of the tool.
    /// </summary>
    ToolRiskLevel RiskLevel { get; }

    /// <summary>
    /// Tool category.
    /// </summary>
    string? Category { get; }

    /// <summary>
    /// Executes the tool.
    /// </summary>
    /// <param name="input">Input parameters (string or JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    Task<ToolResult> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}
