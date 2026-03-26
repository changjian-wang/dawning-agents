namespace Dawning.Agents.Abstractions.Safety;

/// <summary>
/// Guardrail base interface.
/// </summary>
public interface IGuardrail
{
    /// <summary>
    /// Guardrail name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Guardrail description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether the guardrail is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Checks content.
    /// </summary>
    /// <param name="content">Content to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Check result.</returns>
    Task<GuardrailResult> CheckAsync(string content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input guardrail interface - checks user input before LLM calls.
/// </summary>
public interface IInputGuardrail : IGuardrail { }

/// <summary>
/// Output guardrail interface - checks output content after LLM responses.
/// </summary>
public interface IOutputGuardrail : IGuardrail { }

/// <summary>
/// Guardrail pipeline interface - manages execution of multiple guardrails.
/// </summary>
public interface IGuardrailPipeline
{
    /// <summary>
    /// All input guardrails.
    /// </summary>
    IReadOnlyList<IInputGuardrail> InputGuardrails { get; }

    /// <summary>
    /// All output guardrails.
    /// </summary>
    IReadOnlyList<IOutputGuardrail> OutputGuardrails { get; }

    /// <summary>
    /// Checks input content.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Check result.</returns>
    Task<GuardrailResult> CheckInputAsync(
        string input,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks output content.
    /// </summary>
    /// <param name="output">LLM output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Check result.</returns>
    Task<GuardrailResult> CheckOutputAsync(
        string output,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds an input guardrail.
    /// </summary>
    IGuardrailPipeline AddInputGuardrail(IInputGuardrail guardrail);

    /// <summary>
    /// Adds an output guardrail.
    /// </summary>
    IGuardrailPipeline AddOutputGuardrail(IOutputGuardrail guardrail);
}
