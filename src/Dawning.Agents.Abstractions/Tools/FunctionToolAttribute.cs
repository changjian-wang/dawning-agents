namespace Dawning.Agents.Abstractions.Tools;

/// <summary>
/// Marks a method as a tool that can be invoked by an Agent.
/// </summary>
/// <remarks>
/// <para>Methods marked with this attribute are automatically scanned and registered as tools.</para>
/// <para>Method signature requirements:</para>
/// <list type="bullet">
/// <item>Return type: string, Task&lt;string&gt;, ToolResult, or Task&lt;ToolResult&gt;</item>
/// <item>Parameters: basic types supported (string, int, bool, double, etc.)</item>
/// <item>Optional: the last parameter can be a CancellationToken</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [FunctionTool("Search web content")]
/// public string Search(string query) => $"Results for: {query}";
///
/// [FunctionTool("Calculate a mathematical expression")]
/// public async Task&lt;string&gt; CalculateAsync(string expression, CancellationToken ct = default)
/// {
///     // Implementation
/// }
///
/// // High-risk operations require confirmation
/// [FunctionTool("Execute a terminal command", RequiresConfirmation = true, RiskLevel = ToolRiskLevel.High)]
/// public string RunCommand(string command) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FunctionToolAttribute : Attribute
{
    /// <summary>
    /// Tool description (for the LLM to understand the tool's purpose).
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Tool name (optional; defaults to the method name).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether user confirmation is required before execution (for dangerous operations).
    /// </summary>
    /// <remarks>
    /// When set to <see langword="true"/>, user confirmation is requested before tool execution.
    /// Applicable to: file deletion, command execution, system settings modification, etc.
    /// </remarks>
    public bool RequiresConfirmation { get; set; } = false;

    /// <summary>
    /// Risk level of the tool.
    /// </summary>
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.Low;

    /// <summary>
    /// Tool category (used for grouping display).
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Creates a <see cref="FunctionToolAttribute"/>.
    /// </summary>
    /// <param name="description">Tool description.</param>
    public FunctionToolAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// Tool risk level.
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>
    /// Low risk — read-only operations with no side effects (e.g., get time, calculate, format).
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium risk — may have side effects but recoverable (e.g., write file, HTTP request).
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High risk — may cause irreversible impact (e.g., delete file, execute command, Git operations).
    /// </summary>
    High = 2,
}

/// <summary>
/// Marks a parameter's description (for the LLM to understand the parameter's purpose).
/// </summary>
/// <example>
/// <code>
/// [FunctionTool("Search web pages")]
/// public string Search(
///     [ToolParameter("Search keywords")] string query,
///     [ToolParameter("Number of results to return")] int count = 10)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ToolParameterAttribute : Attribute
{
    /// <summary>
    /// Parameter description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates a <see cref="ToolParameterAttribute"/>.
    /// </summary>
    /// <param name="description">Parameter description.</param>
    public ToolParameterAttribute(string description)
    {
        Description = description;
    }
}
