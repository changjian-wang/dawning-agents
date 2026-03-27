using System.Collections.Concurrent;
using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Handoff;

/// <summary>
/// Handoff handler implementation - manages task transfers between agents.
/// </summary>
public sealed class HandoffHandler : IHandoffHandler
{
    private readonly ConcurrentDictionary<string, IAgent> _agents = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly HandoffOptions _options;
    private readonly ILogger<HandoffHandler> _logger;

    public HandoffHandler(
        IOptions<HandoffOptions>? options = null,
        ILogger<HandoffHandler>? logger = null
    )
    {
        _options = options?.Value ?? new HandoffOptions();
        _logger = logger ?? NullLogger<HandoffHandler>.Instance;
    }

    /// <inheritdoc />
    public void RegisterAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (_agents.TryAdd(agent.Name, agent))
        {
            _logger.LogDebug("Registered agent: {AgentName}", agent.Name);
        }
        else
        {
            _logger.LogWarning("Agent already registered: {AgentName}", agent.Name);
        }
    }

    /// <inheritdoc />
    public void RegisterAgents(IEnumerable<IAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);
        foreach (var agent in agents)
        {
            RegisterAgent(agent);
        }
    }

    /// <inheritdoc />
    public IAgent? GetAgent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _agents.TryGetValue(name, out var agent) ? agent : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IAgent> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <inheritdoc />
    public async Task<HandoffResult> ExecuteHandoffAsync(
        HandoffRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var chain = new List<HandoffRecord>();

        // Add initial handoff record
        chain.Add(
            new HandoffRecord
            {
                FromAgent = null,
                ToAgent = request.TargetAgentName,
                Reason = request.Reason,
                Input = request.Input,
            }
        );

        var result = await ExecuteHandoffChainAsync(
                request.TargetAgentName,
                request.Input,
                chain,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                cancellationToken
            )
            .ConfigureAwait(false);

        stopwatch.Stop();

        return result with
        {
            TotalDuration = stopwatch.Elapsed,
        };
    }

    /// <inheritdoc />
    public async Task<HandoffResult> RunWithHandoffAsync(
        string entryAgentName,
        string input,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryAgentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var request = HandoffRequest.To(entryAgentName, input);
        return await ExecuteHandoffAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recursively executes the handoff chain.
    /// </summary>
    private async Task<HandoffResult> ExecuteHandoffChainAsync(
        string agentName,
        string input,
        List<HandoffRecord> chain,
        HashSet<string> visitedAgents,
        CancellationToken cancellationToken
    )
    {
        // Check depth limit (chain includes entry record, actual handoff count = Count - 1)
        if (chain.Count - 1 > _options.MaxHandoffDepth)
        {
            _logger.LogWarning(
                "Handoff depth limit exceeded. Max: {MaxDepth}, Current: {CurrentDepth}",
                _options.MaxHandoffDepth,
                chain.Count
            );

            return HandoffResult.Failed(
                agentName,
                $"Handoff depth limit exceeded (max: {_options.MaxHandoffDepth})",
                chain,
                TimeSpan.Zero
            );
        }

        // Check for cycles
        if (!_options.AllowCycles && visitedAgents.Contains(agentName))
        {
            _logger.LogWarning("Handoff cycle detected: {AgentName}", agentName);

            return HandoffResult.Failed(
                agentName,
                $"Handoff cycle detected: {agentName} was already visited",
                chain,
                TimeSpan.Zero
            );
        }

        // Get target agent
        var agent = GetAgent(agentName);
        if (agent == null)
        {
            _logger.LogError("Target agent not found: {AgentName}", agentName);

            return HandoffResult.Failed(
                agentName,
                $"Agent not found: {agentName}",
                chain,
                TimeSpan.Zero
            );
        }

        visitedAgents.Add(agentName);

        _logger.LogInformation(
            "Executing handoff to agent: {AgentName}, Input: {Input}",
            agentName,
            input.Length > 100 ? input[..100] + "..." : input
        );

        try
        {
            // Create timeout token
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.TimeoutSeconds)
            );
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            // Execute agent
            var response = await agent.RunAsync(input, linkedCts.Token).ConfigureAwait(false);

            // Check if handoff continuation is needed
            if (response.IsHandoffRequest())
            {
                var handoffRequest = response.ParseHandoffRequest();
                if (handoffRequest != null)
                {
                    _logger.LogInformation(
                        "Agent {AgentName} requested handoff to {TargetAgent}. Reason: {Reason}",
                        agentName,
                        handoffRequest.TargetAgentName,
                        handoffRequest.Reason ?? "N/A"
                    );

                    // Validate handoff target is allowed
                    if (agent is IHandoffAgent handoffAgent)
                    {
                        if (
                            handoffAgent.Handoffs.Count > 0
                            && !handoffAgent.Handoffs.Contains(
                                handoffRequest.TargetAgentName,
                                StringComparer.OrdinalIgnoreCase
                            )
                        )
                        {
                            _logger.LogWarning(
                                "Agent {AgentName} is not allowed to handoff to {TargetAgent}",
                                agentName,
                                handoffRequest.TargetAgentName
                            );

                            return HandoffResult.Failed(
                                agentName,
                                $"Agent {agentName} is not allowed to handoff to {handoffRequest.TargetAgentName}",
                                chain,
                                TimeSpan.Zero
                            );
                        }
                    }

                    // Add handoff record
                    chain.Add(
                        new HandoffRecord
                        {
                            FromAgent = agentName,
                            ToAgent = handoffRequest.TargetAgentName,
                            Reason = handoffRequest.Reason,
                            Input = handoffRequest.Input,
                        }
                    );

                    // Recursively execute next agent (pass linkedCts.Token to maintain timeout constraint)
                    return await ExecuteHandoffChainAsync(
                            handoffRequest.TargetAgentName,
                            handoffRequest.Input,
                            chain,
                            visitedAgents,
                            linkedCts.Token
                        )
                        .ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning(
                        "Agent {AgentName} produced malformed handoff response",
                        agentName
                    );

                    return HandoffResult.Failed(
                        agentName,
                        "Malformed handoff response: unable to parse handoff request",
                        chain,
                        TimeSpan.Zero
                    );
                }
            }

            // Return final result
            return HandoffResult.Successful(agentName, response, chain, TimeSpan.Zero);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Handoff was cancelled");
            return HandoffResult.Failed(agentName, "Operation was cancelled", chain, TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Handoff to {AgentName} timed out", agentName);
            return HandoffResult.Failed(
                agentName,
                $"Agent {agentName} execution timed out after {_options.TimeoutSeconds}s",
                chain,
                TimeSpan.Zero
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handoff to {AgentName} failed", agentName);

            if (_options.FallbackToSource && chain.Count > 1)
            {
                var previousRecord = chain[^2];
                if (previousRecord.FromAgent != null)
                {
                    _logger.LogInformation(
                        "Falling back to source agent: {AgentName}",
                        previousRecord.FromAgent
                    );
                    // Fallback logic can be implemented here
                }
            }

            return HandoffResult.Failed(agentName, ex.Message, chain, TimeSpan.Zero);
        }
    }
}
