namespace Dawning.Agents.Core.Orchestration;

using System.Text;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Parallel orchestrator that executes multiple agents concurrently and aggregates results.
/// </summary>
/// <remarks>
/// Execution flow:
/// <code>
///           ┌─→ Agent A ─→ Result A ─┐
/// Input ────┼─→ Agent B ─→ Result B ─┼──→ Aggregator → Output
///           └─→ Agent C ─→ Result C ─┘
/// </code>
///
/// Usage scenarios:
/// <list type="bullet">
///   <item>Multi-perspective analysis: multiple expert agents analyze the problem concurrently.</item>
///   <item>Redundant execution: multiple agents perform the same task, selecting the best result.</item>
///   <item>Divide and conquer: decompose a large task for parallel processing by multiple agents.</item>
/// </list>
/// </remarks>
public sealed class ParallelOrchestrator : OrchestratorBase
{
    /// <summary>
    /// Custom result aggregator.
    /// </summary>
    private Func<IReadOnlyList<AgentExecutionRecord>, string>? _customAggregator;

    /// <summary>
    /// Local aggregation strategy (avoids mutating shared options).
    /// </summary>
    private ResultAggregationStrategy? _localAggregationStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelOrchestrator"/> class.
    /// </summary>
    public ParallelOrchestrator(
        string name,
        IOptions<OrchestratorOptions>? options = null,
        ILogger<ParallelOrchestrator>? logger = null
    )
        : base(name, options, logger)
    {
        Description = "Executes multiple agents in parallel and aggregates the results";
    }

    /// <summary>
    /// Sets a custom result aggregator.
    /// </summary>
    public ParallelOrchestrator WithAggregator(
        Func<IReadOnlyList<AgentExecutionRecord>, string> aggregator
    )
    {
        _customAggregator = aggregator;
        _localAggregationStrategy = ResultAggregationStrategy.Custom;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<OrchestrationResult> ExecuteOrchestratedAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken
    )
    {
        var input = context.CurrentInput;

        // Throttle concurrency with SemaphoreSlim
        using var semaphore = new SemaphoreSlim(Options.MaxConcurrency);

        var agents = Volatile.Read(ref _agents);
        var tasks = agents
            .Select(
                async (agent, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        return await ExecuteAgentAsync(agent, input, index, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Per-task error handling: capture the failure as a record
                        // so partial results from other agents are not lost
                        return new AgentExecutionRecord
                        {
                            AgentName = agent.Name,
                            Input = input,
                            Response = AgentResponse.Failed(ex.Message, [], TimeSpan.Zero, ex),
                            ExecutionOrder = index,
                            StartTime = DateTimeOffset.UtcNow,
                            EndTime = DateTimeOffset.UtcNow,
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            )
            .ToList();

        AgentExecutionRecord[] results;

        try
        {
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // This should rarely happen now since per-task errors are caught above,
            // but guard against unexpected aggregate exceptions
            Logger.LogError(ex, "Unexpected error during parallel agent execution");

            if (!Options.ContinueOnError)
            {
                return OrchestrationResult.Failed(
                    $"Parallel execution failed: {ex.Message}",
                    context.ExecutionHistory,
                    TimeSpan.Zero
                );
            }

            // Preserve partial results from tasks that completed successfully
            results = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();
        }

        // Add to execution history
        foreach (var record in results)
        {
            context.AddExecutionRecord(record);
        }

        // Check whether all agents failed
        var successfulResults = results.Where(r => r.Response.Success).ToList();
        if (successfulResults.Count == 0 && results.Length > 0)
        {
            return OrchestrationResult.Failed(
                "All agents failed",
                context.ExecutionHistory,
                TimeSpan.Zero
            );
        }

        // Aggregate results
        var finalOutput = AggregateResults(results);

        return OrchestrationResult.Successful(
            finalOutput,
            context.ExecutionHistory,
            TimeSpan.Zero,
            new Dictionary<string, object>(context.Metadata)
        );
    }

    /// <summary>
    /// Aggregates results according to the configured strategy.
    /// </summary>
    private string AggregateResults(AgentExecutionRecord[] results)
    {
        if (results.Length == 0)
        {
            return string.Empty;
        }

        return (_localAggregationStrategy ?? Options.AggregationStrategy) switch
        {
            ResultAggregationStrategy.LastResult => AggregateLastResult(results),
            ResultAggregationStrategy.FirstSuccess => AggregateFirstSuccess(results),
            ResultAggregationStrategy.Merge => AggregateMerge(results),
            ResultAggregationStrategy.Vote => AggregateVote(results),
            ResultAggregationStrategy.Custom when _customAggregator != null => _customAggregator(
                results
            ),
            _ => AggregateLastResult(results),
        };
    }

    private static string AggregateLastResult(AgentExecutionRecord[] results)
    {
        var lastSuccess = results.LastOrDefault(r => r.Response.Success);
        return lastSuccess?.Response.FinalAnswer
            ?? results.Last().Response.FinalAnswer
            ?? string.Empty;
    }

    private static string AggregateFirstSuccess(AgentExecutionRecord[] results)
    {
        var firstSuccess = results.FirstOrDefault(r => r.Response.Success);
        return firstSuccess?.Response.FinalAnswer ?? string.Empty;
    }

    private static string AggregateMerge(AgentExecutionRecord[] results)
    {
        var sb = new StringBuilder();

        foreach (var result in results.Where(r => r.Response.Success))
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine($"[{result.AgentName}]:");
            sb.AppendLine(result.Response.FinalAnswer);
        }

        return sb.ToString().TrimEnd();
    }

    private static string AggregateVote(AgentExecutionRecord[] results)
    {
        // Simple voting: select the most frequent answer
        var answers = results
            .Where(r => r.Response.Success && !string.IsNullOrWhiteSpace(r.Response.FinalAnswer))
            .GroupBy(r => r.Response.FinalAnswer!.Trim())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return answers?.Key ?? string.Empty;
    }
}
