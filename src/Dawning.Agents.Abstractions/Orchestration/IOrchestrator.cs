namespace Dawning.Agents.Abstractions.Orchestration;

using Dawning.Agents.Abstractions.Agent;

/// <summary>
/// Agent orchestrator interface, responsible for coordinating the execution of multiple Agents.
/// </summary>
/// <remarks>
/// The orchestrator is the core component of multi-Agent systems, supporting the following scenarios:
/// <list type="bullet">
/// <item>Sequential execution: Agent A → Agent B → Agent C</item>
/// <item>Parallel execution: running multiple Agents simultaneously and aggregating results</item>
/// <item>Conditional routing: dynamically selecting the next Agent based on results</item>
/// <item>Handoff: task transfer between Agents</item>
/// </list>
/// </remarks>
public interface IOrchestrator
{
    /// <summary>
    /// Orchestrator name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Orchestrator description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// All Agents participating in orchestration.
    /// </summary>
    IReadOnlyList<IAgent> Agents { get; }

    /// <summary>
    /// Executes the orchestration flow.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Orchestration result.</returns>
    Task<OrchestrationResult> RunAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the orchestration flow with context.
    /// </summary>
    /// <param name="context">Orchestration context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Orchestration result.</returns>
    Task<OrchestrationResult> RunAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default
    );
}
