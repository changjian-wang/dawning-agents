using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Reflection engine — diagnosis and repair decisions after tool execution failures.
/// </summary>
/// <remarks>
/// <para>Inspired by Memento-Skills Read-Write Reflective Learning.</para>
/// <para>Failures are not just retry signals, but training signals.</para>
/// </remarks>
public interface IReflectionEngine
{
    /// <summary>
    /// Reflects on a failed tool execution and produces a repair strategy.
    /// </summary>
    /// <param name="context">Reflection context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ReflectionResult> ReflectAsync(
        ReflectionContext context,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Reflection context.
/// </summary>
public record ReflectionContext
{
    /// <summary>
    /// The failed tool.
    /// </summary>
    public required ITool FailedTool { get; init; }

    /// <summary>
    /// Input parameters for the tool.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// The failed execution result.
    /// </summary>
    public required ToolResult FailedResult { get; init; }

    /// <summary>
    /// Original task description.
    /// </summary>
    public required string TaskDescription { get; init; }

    /// <summary>
    /// Previous execution steps.
    /// </summary>
    public IReadOnlyList<AgentStep>? PreviousSteps { get; init; }

    /// <summary>
    /// Tool usage statistics.
    /// </summary>
    public ToolUsageStats? UsageStats { get; init; }
}

/// <summary>
/// Reflection result.
/// </summary>
public record ReflectionResult
{
    /// <summary>
    /// Suggested repair strategy.
    /// </summary>
    public required ReflectionAction Action { get; init; }

    /// <summary>
    /// Revised tool definition (when Action is <see cref="ReflectionAction.ReviseAndRetry"/>).
    /// </summary>
    public EphemeralToolDefinition? RevisedDefinition { get; init; }

    /// <summary>
    /// Diagnostic report.
    /// </summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    public float Confidence { get; init; }
}

/// <summary>
/// Reflection repair strategy.
/// </summary>
public enum ReflectionAction
{
    /// <summary>
    /// Simple retry (transient errors such as network timeouts).
    /// </summary>
    Retry,

    /// <summary>
    /// Revise tool definition and retry.
    /// </summary>
    ReviseAndRetry,

    /// <summary>
    /// Abandon the tool and select an alternative.
    /// </summary>
    Abandon,

    /// <summary>
    /// Create a new tool.
    /// </summary>
    CreateNew,

    /// <summary>
    /// Escalate to a human.
    /// </summary>
    Escalate,
}
