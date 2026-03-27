namespace Dawning.Agents.Core.Orchestration;

using System.Collections.Immutable;
using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Base class for orchestrators providing common functionality.
/// </summary>
public abstract class OrchestratorBase : IOrchestrator
{
    /// <summary>
    /// The logger instance.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The orchestrator options.
    /// </summary>
    protected readonly OrchestratorOptions Options;

    /// <summary>
    /// The list of registered agents.
    /// </summary>
    protected ImmutableList<IAgent> _agents = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorBase"/> class.
    /// </summary>
    protected OrchestratorBase(
        string name,
        IOptions<OrchestratorOptions>? options = null,
        ILogger? logger = null
    )
    {
        Name = name;
        Options = options?.Value ?? new OrchestratorOptions();
        Logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public virtual string? Description { get; protected set; }

    /// <inheritdoc />
    public IReadOnlyList<IAgent> Agents => Volatile.Read(ref _agents);

    /// <summary>
    /// Adds an agent to the orchestrator.
    /// </summary>
    public OrchestratorBase AddAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ImmutableInterlocked.Update(ref _agents, (list, a) => list.Add(a), agent);
        Logger.LogDebug("Agent {AgentName} added to orchestrator {OrchestratorName}", agent.Name, Name);
        return this;
    }

    /// <summary>
    /// Adds multiple agents to the orchestrator.
    /// </summary>
    public OrchestratorBase AddAgents(IEnumerable<IAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        foreach (var agent in agents)
        {
            AddAgent(agent);
        }
        return this;
    }

    /// <inheritdoc />
    public async Task<OrchestrationResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new OrchestrationContext { UserInput = input, CurrentInput = input };

        return await RunAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OrchestrationResult> RunAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Volatile.Read(ref _agents).Count == 0)
        {
            return OrchestrationResult.Failed("No agents registered in the orchestrator", [], TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Options.TimeoutSeconds)
        );
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token
        );

        try
        {
            Logger.LogInformation(
                "Orchestrator {OrchestratorName} starting execution with {AgentCount} agents",
                Name,
                Volatile.Read(ref _agents).Count
            );

            var result = await ExecuteOrchestratedAsync(context, linkedCts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();

            Logger.LogInformation(
                "Orchestrator {OrchestratorName} completed in {Duration}ms, success: {Success}",
                Name,
                stopwatch.ElapsedMilliseconds,
                result.Success
            );

            return result with
            {
                Duration = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogWarning("Orchestrator {OrchestratorName} timed out", Name);

            return OrchestrationResult.Failed(
                $"Orchestration timed out ({Options.TimeoutSeconds}s)",
                context.ExecutionHistory,
                stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Orchestrator {OrchestratorName} encountered an error", Name);

            return OrchestrationResult.Failed(
                ex.Message,
                context.ExecutionHistory,
                stopwatch.Elapsed
            );
        }
    }

    /// <summary>
    /// Executes the orchestration logic. Implemented by derived classes.
    /// </summary>
    protected abstract Task<OrchestrationResult> ExecuteOrchestratedAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Executes a single agent.
    /// </summary>
    protected async Task<AgentExecutionRecord> ExecuteAgentAsync(
        IAgent agent,
        string input,
        int executionOrder,
        CancellationToken cancellationToken
    )
    {
        var startTime = DateTimeOffset.UtcNow;

        using var agentCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(Options.AgentTimeoutSeconds)
        );
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            agentCts.Token
        );

        Logger.LogDebug("Executing agent {AgentName}, order: {Order}", agent.Name, executionOrder);

        AgentResponse response;
        try
        {
            response = await agent.RunAsync(input, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (agentCts.IsCancellationRequested)
        {
            response = AgentResponse.Failed(
                $"Agent {agent.Name} timed out ({Options.AgentTimeoutSeconds}s)",
                [],
                TimeSpan.FromSeconds(Options.AgentTimeoutSeconds)
            );
        }
        catch (Exception ex)
        {
            response = AgentResponse.Failed(ex.Message, [], TimeSpan.Zero, ex);
        }

        var endTime = DateTimeOffset.UtcNow;

        Logger.LogDebug(
            "Agent {AgentName} completed, success: {Success}",
            agent.Name,
            response.Success
        );

        return new AgentExecutionRecord
        {
            AgentName = agent.Name,
            Input = input,
            Response = response,
            ExecutionOrder = executionOrder,
            StartTime = startTime,
            EndTime = endTime,
        };
    }
}
