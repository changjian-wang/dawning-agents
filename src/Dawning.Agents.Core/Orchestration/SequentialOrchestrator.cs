namespace Dawning.Agents.Core.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Sequential orchestrator that executes agents in order, passing each output as the next input.
/// </summary>
/// <remarks>
/// Execution flow:
/// <code>
/// Input → Agent A → Agent B → Agent C → Output
///         ↓          ↓          ↓
///       Result A   Result B   Result C (Final)
/// </code>
///
/// Usage scenarios:
/// <list type="bullet">
///   <item>Pipeline processing: translate -> refine -> format.</item>
///   <item>Step-by-step reasoning: analyze -> plan -> execute.</item>
///   <item>Data transformation: extract -> clean -> store.</item>
/// </list>
/// </remarks>
public sealed class SequentialOrchestrator : OrchestratorBase
{
    /// <summary>
    /// Input transformer that converts output before passing it to the next agent.
    /// </summary>
    private Func<AgentExecutionRecord, string>? _inputTransformer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialOrchestrator"/> class.
    /// </summary>
    public SequentialOrchestrator(
        string name,
        IOptions<OrchestratorOptions>? options = null,
        ILogger<SequentialOrchestrator>? logger = null
    )
        : base(name, options, logger)
    {
        Description = "Executes agents sequentially, passing each output as the next input";
    }

    /// <summary>
    /// Sets the input transformer.
    /// </summary>
    /// <param name="transformer">Transform function that converts an agent execution record to the next agent's input.</param>
    public SequentialOrchestrator WithInputTransformer(
        Func<AgentExecutionRecord, string> transformer
    )
    {
        _inputTransformer = transformer;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<OrchestrationResult> ExecuteOrchestratedAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken
    )
    {
        var currentInput = context.CurrentInput;
        var agents = Volatile.Read(ref _agents);

        for (var i = 0; i < agents.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.ShouldStop)
            {
                Logger.LogInformation(
                    "Orchestration stopped at agent {Index}/{Total}, reason: {Reason}",
                    i + 1,
                    agents.Count,
                    context.StopReason
                );
                break;
            }

            var agent = agents[i];
            var record = await ExecuteAgentAsync(agent, currentInput, i, cancellationToken)
                .ConfigureAwait(false);
            context.AddExecutionRecord(record);

            if (!record.Response.Success)
            {
                if (!Options.ContinueOnError)
                {
                    return OrchestrationResult.Failed(
                        $"Agent {agent.Name} failed: {record.Response.Error}",
                        context.ExecutionHistory,
                        TimeSpan.Zero
                    );
                }

                Logger.LogWarning(
                    "Agent {AgentName} failed, but continuing to the next agent",
                    agent.Name
                );
            }
            else
            {
                // Transform output as input for the next agent
                var transformed =
                    _inputTransformer != null
                        ? _inputTransformer(record)
                        : record.Response.FinalAnswer;

                currentInput = transformed ?? currentInput;
                context.CurrentInput = currentInput;
            }
        }

        // Get final result: prefer the last successful answer
        var executionHistory = context.ExecutionHistory;
        var lastSuccessRecord = executionHistory.LastOrDefault(r => r.Response.Success);

        // Return failure when ContinueOnError is enabled and all agents failed
        if (lastSuccessRecord == null && executionHistory.Count > 0)
        {
            return OrchestrationResult.Failed("All agents failed", executionHistory, TimeSpan.Zero);
        }

        var finalOutput =
            lastSuccessRecord?.Response.FinalAnswer
            ?? executionHistory.LastOrDefault()?.Response.FinalAnswer
            ?? string.Empty;

        return OrchestrationResult.Successful(
            finalOutput,
            executionHistory,
            TimeSpan.Zero,
            new Dictionary<string, object>(context.Metadata)
        );
    }
}
