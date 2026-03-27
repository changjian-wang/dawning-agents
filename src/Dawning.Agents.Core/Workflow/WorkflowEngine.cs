using System.Diagnostics;
using System.Text.Json;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Abstractions.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Workflow;

/// <summary>
/// Workflow execution engine.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IToolReader? _toolRegistry;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IServiceProvider serviceProvider,
        IToolReader? toolRegistry = null,
        ILogger<WorkflowEngine>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
        _toolRegistry = toolRegistry;
        _logger = logger ?? NullLogger<WorkflowEngine>.Instance;
    }

    /// <inheritdoc />
    public IWorkflow CreateWorkflow(WorkflowDefinition definition)
    {
        var validation = Validate(definition);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Invalid workflow definition: {errors}");
        }

        return new ExecutableWorkflow(definition, this);
    }

    /// <inheritdoc />
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowContext context,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var nodeMap = definition.Nodes.ToDictionary(n => n.Id);
        var currentNodeId = definition.StartNodeId;
        const int maxExecutionSteps = 1000;
        var executionStepCount = 0;

        _logger.LogInformation(
            "Starting workflow {WorkflowId}: {WorkflowName}",
            definition.Id,
            definition.Name
        );

        try
        {
            while (
                !string.IsNullOrEmpty(currentNodeId) && !cancellationToken.IsCancellationRequested
            )
            {
                if (++executionStepCount > maxExecutionSteps)
                {
                    return CreateFailedResult(
                        definition.Id,
                        $"Workflow exceeded maximum execution steps ({maxExecutionSteps}), possible cycle detected",
                        stopwatch,
                        context
                    );
                }

                if (!nodeMap.TryGetValue(currentNodeId, out var nodeDefinition))
                {
                    return CreateFailedResult(
                        definition.Id,
                        $"Node not found: {currentNodeId}",
                        stopwatch,
                        context
                    );
                }

                // Record execution step start
                var step = new WorkflowExecutionStep
                {
                    NodeId = nodeDefinition.Id,
                    NodeName = nodeDefinition.Name,
                    NodeType = nodeDefinition.Type,
                    StartedAt = DateTimeOffset.UtcNow,
                };

                _logger.LogDebug(
                    "Executing node {NodeId}: {NodeName} ({NodeType})",
                    nodeDefinition.Id,
                    nodeDefinition.Name,
                    nodeDefinition.Type
                );

                // Execute node
                var result = await ExecuteNodeAsync(nodeDefinition, context, cancellationToken)
                    .ConfigureAwait(false);
                context.AddNodeResult(nodeDefinition.Id, result);

                // Update execution step
                context.AddExecutionStep(
                    step with
                    {
                        CompletedAt = DateTimeOffset.UtcNow,
                        Success = result.Success,
                    }
                );

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Node {NodeId} failed: {Error}",
                        nodeDefinition.Id,
                        result.Error
                    );
                    return CreateFailedResult(
                        definition.Id,
                        result.Error ?? "Node execution failed",
                        stopwatch,
                        context
                    );
                }

                // Determine next node
                currentNodeId = DetermineNextNode(nodeDefinition, result, definition.Edges);

                // End node
                if (nodeDefinition.Type == WorkflowNodeType.End)
                {
                    break;
                }
            }

            stopwatch.Stop();

            // Unified cancellation semantics: always throw OperationCanceledException
            cancellationToken.ThrowIfCancellationRequested();

            var finalOutput = context.GetLastResult()?.Output;

            _logger.LogInformation(
                "Workflow {WorkflowId} completed in {Duration}ms, executed {NodeCount} nodes",
                definition.Id,
                stopwatch.ElapsedMilliseconds,
                context.ExecutionHistory.Count
            );

            return new WorkflowResult
            {
                WorkflowId = definition.Id,
                Success = true,
                FinalOutput = finalOutput,
                NodesExecuted = context.ExecutionHistory.Count,
                TotalDurationMs = stopwatch.ElapsedMilliseconds,
                ExecutionHistory = context.ExecutionHistory,
                FinalState = context.State,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Workflow {WorkflowId} was canceled", definition.Id);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Workflow {WorkflowId} was canceled", definition.Id);
            return CreateFailedResult(definition.Id, "Workflow execution was canceled", stopwatch, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowId} encountered an exception", definition.Id);
            return CreateFailedResult(definition.Id, ex.Message, stopwatch, context);
        }
    }

    /// <inheritdoc />
    public WorkflowValidationResult Validate(WorkflowDefinition definition)
    {
        var errors = new List<WorkflowValidationError>();
        var warnings = new List<WorkflowValidationWarning>();
        var nodeIds = definition.Nodes.Select(n => n.Id).ToHashSet();

        // Validate start node
        if (!nodeIds.Contains(definition.StartNodeId))
        {
            errors.Add(
                new WorkflowValidationError
                {
                    Code = "INVALID_START_NODE",
                    Message = $"Start node '{definition.StartNodeId}' does not exist",
                }
            );
        }

        // Validate edges
        foreach (var edge in definition.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId))
            {
                errors.Add(
                    new WorkflowValidationError
                    {
                        Code = "INVALID_EDGE_SOURCE",
                        Message = $"Edge source node '{edge.FromNodeId}' does not exist",
                    }
                );
            }

            if (!nodeIds.Contains(edge.ToNodeId))
            {
                errors.Add(
                    new WorkflowValidationError
                    {
                        Code = "INVALID_EDGE_TARGET",
                        Message = $"Edge target node '{edge.ToNodeId}' does not exist",
                    }
                );
            }
        }

        // Validate end node
        var hasEndNode = definition.Nodes.Any(n => n.Type == WorkflowNodeType.End);
        if (!hasEndNode)
        {
            warnings.Add(
                new WorkflowValidationWarning
                {
                    Code = "NO_END_NODE",
                    Message = "Workflow has no end node, which may cause infinite execution",
                }
            );
        }

        // Check for orphan nodes
        var referencedNodes = definition
            .Edges.SelectMany(e => new[] { e.FromNodeId, e.ToNodeId })
            .ToHashSet();
        referencedNodes.Add(definition.StartNodeId);

        foreach (var node in definition.Nodes)
        {
            if (!referencedNodes.Contains(node.Id) && node.Type != WorkflowNodeType.End)
            {
                warnings.Add(
                    new WorkflowValidationWarning
                    {
                        Code = "ORPHAN_NODE",
                        Message = $"Node '{node.Id}' is not referenced by any edge",
                        NodeId = node.Id,
                    }
                );
            }

            // Check for unsupported node types
            if (node.Type is WorkflowNodeType.Parallel or WorkflowNodeType.Loop)
            {
                errors.Add(
                    new WorkflowValidationError
                    {
                        Code = "UNSUPPORTED_NODE_TYPE",
                        Message = $"Node '{node.Id}' type {node.Type} is not yet implemented",
                    }
                );
            }
        }

        return new WorkflowValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = nodeDefinition.Type switch
            {
                WorkflowNodeType.Start => NodeExecutionResult.Ok(nodeDefinition.Id),
                WorkflowNodeType.End => NodeExecutionResult.Ok(
                    nodeDefinition.Id,
                    context.GetLastResult()?.Output
                ),
                WorkflowNodeType.Agent => await ExecuteAgentNodeAsync(
                        nodeDefinition,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false),
                WorkflowNodeType.Tool => await ExecuteToolNodeAsync(
                        nodeDefinition,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false),
                WorkflowNodeType.Condition => ExecuteConditionNode(nodeDefinition, context),
                WorkflowNodeType.Delay => await ExecuteDelayNodeAsync(
                        nodeDefinition,
                        cancellationToken
                    )
                    .ConfigureAwait(false),
                WorkflowNodeType.Parallel => await ExecuteParallelNodeAsync(
                        nodeDefinition,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false),
                WorkflowNodeType.Loop => await ExecuteLoopNodeAsync(
                        nodeDefinition,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false),
                _ => NodeExecutionResult.Fail(
                    nodeDefinition.Id,
                    $"Unsupported node type: {nodeDefinition.Type}"
                ),
            };

            stopwatch.Stop();
            return result with { DurationMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return NodeExecutionResult.Fail(nodeDefinition.Id, ex.Message) with
            {
                DurationMs = stopwatch.ElapsedMilliseconds,
            };
        }
    }

    private async Task<NodeExecutionResult> ExecuteAgentNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        var config = nodeDefinition.Config;
        if (config == null || !config.TryGetValue("agentName", out var agentNameObj))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Agent node is missing agentName configuration");
        }

        var agentName = agentNameObj?.ToString();
        if (string.IsNullOrEmpty(agentName))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Agent name cannot be empty");
        }

        // Resolve agent from DI by name (create scope to resolve scoped services)
        using var scope = _serviceProvider.CreateScope();
        var agents = scope.ServiceProvider.GetServices<IAgent>();
        var agent = agents.FirstOrDefault(a =>
            string.Equals(a.Name, agentName, StringComparison.OrdinalIgnoreCase)
        );
        if (agent == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, $"Agent not found: {agentName}");
        }

        // Build input
        var input = context.GetLastResult()?.Output ?? context.Input;
        var template = GetConfigString(config, "inputTemplate");
        if (template != null)
        {
            input = ReplaceVariables(template, context);
        }

        // Execute agent
        var response = await agent.RunAsync(input, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.Success)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, response.Error ?? "Agent execution failed");
        }

        return NodeExecutionResult.Ok(nodeDefinition.Id, response.FinalAnswer);
    }

    private async Task<NodeExecutionResult> ExecuteToolNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        var config = nodeDefinition.Config;
        if (config == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Tool node is missing configuration");
        }

        var toolName = GetConfigString(config, "toolName");
        if (string.IsNullOrEmpty(toolName))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Tool node is missing toolName configuration");
        }

        if (_toolRegistry == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Tool registry is not configured");
        }

        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, $"Tool not found: {toolName}");
        }

        // Build input
        var input = context.GetLastResult()?.Output ?? context.Input;
        var template = GetConfigString(config, "inputTemplate");
        if (template != null)
        {
            input = ReplaceVariables(template, context);
        }

        // Execute tool
        var toolResult = await tool.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (toolResult.Success)
        {
            return NodeExecutionResult.Ok(nodeDefinition.Id, toolResult.Output);
        }
        else
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, toolResult.Error ?? "Tool execution failed");
        }
    }

    private NodeExecutionResult ExecuteConditionNode(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context
    )
    {
        var config = nodeDefinition.Config;
        if (config == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Condition node is missing configuration");
        }

        // Get input value
        var inputValue = context.GetLastResult()?.Output ?? context.Input;
        var inputSource = GetConfigString(config, "inputSource");
        if (inputSource != null)
        {
            inputValue = context.GetState<string>(inputSource) ?? inputValue;
        }

        // Evaluate branch conditions
        var branches = GetConfigBranches(config, "branches");
        if (branches != null)
        {
            foreach (var branch in branches)
            {
                if (
                    branch.TryGetValue("condition", out var condition)
                    && EvaluateCondition(condition, inputValue, context)
                )
                {
                    if (branch.TryGetValue("targetNodeId", out var targetNodeId))
                    {
                        return NodeExecutionResult.Branch(nodeDefinition.Id, targetNodeId);
                    }
                }
            }
        }

        // Default branch
        var defaultNodeId = GetConfigString(config, "defaultBranchNodeId");
        if (defaultNodeId != null)
        {
            return NodeExecutionResult.Branch(nodeDefinition.Id, defaultNodeId);
        }

        return NodeExecutionResult.Fail(nodeDefinition.Id, "Condition node has no matching branch");
    }

    private async Task<NodeExecutionResult> ExecuteDelayNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        CancellationToken cancellationToken
    )
    {
        var config = nodeDefinition.Config;
        var delayMs = GetConfigInt(config ?? new Dictionary<string, object?>(), "delayMs") ?? 1000;

        delayMs = Math.Max(0, delayMs);

        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return NodeExecutionResult.Ok(nodeDefinition.Id);
    }

    private Task<NodeExecutionResult> ExecuteParallelNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException(
            $"Parallel node '{nodeDefinition.Id}' is not implemented. Do not add Parallel type nodes to workflows."
        );
    }

    private Task<NodeExecutionResult> ExecuteLoopNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException(
            $"Loop node '{nodeDefinition.Id}' is not implemented. Do not add Loop type nodes to workflows."
        );
    }

    private string? DetermineNextNode(
        WorkflowNodeDefinition currentNode,
        NodeExecutionResult result,
        IReadOnlyList<WorkflowEdgeDefinition> edges
    )
    {
        // If the node specified the next node (conditional branch)
        if (!string.IsNullOrEmpty(result.NextNodeId))
        {
            return result.NextNodeId;
        }

        // Find outgoing edge
        var outgoingEdge = edges.FirstOrDefault(e => e.FromNodeId == currentNode.Id);
        return outgoingEdge?.ToNodeId;
    }

    private bool EvaluateCondition(string condition, string? inputValue, WorkflowContext context)
    {
        if (string.IsNullOrEmpty(inputValue))
        {
            // When inputValue is null/empty, only state: conditions are supported
            var stateParts = condition.Split(':', 2);
            return stateParts.Length == 2
                && stateParts[0].Equals("state", StringComparison.OrdinalIgnoreCase)
                && context.GetState<string>(stateParts[1]) != null;
        }

        // Simple condition evaluation
        // Supports: contains:xxx, equals:xxx, startsWith:xxx, endsWith:xxx
        var parts = condition.Split(':', 2);
        if (parts.Length != 2)
        {
            return inputValue.Contains(condition, StringComparison.OrdinalIgnoreCase);
        }

        var op = parts[0].ToLowerInvariant();
        var value = parts[1];

        return op switch
        {
            "contains" => inputValue.Contains(value, StringComparison.OrdinalIgnoreCase),
            "equals" => inputValue.Equals(value, StringComparison.OrdinalIgnoreCase),
            "startswith" => inputValue.StartsWith(value, StringComparison.OrdinalIgnoreCase),
            "endswith" => inputValue.EndsWith(value, StringComparison.OrdinalIgnoreCase),
            "state" => context.GetState<string>(value) != null,
            _ => false,
        };
    }

    private string ReplaceVariables(string template, WorkflowContext context)
    {
        var result = template;

        // Replace {{input}}
        result = result.Replace("{{input}}", context.Input ?? "");

        // Replace {{lastOutput}}
        result = result.Replace("{{lastOutput}}", context.GetLastResult()?.Output ?? "");

        // Replace {{state.xxx}}
        foreach (var kvp in context.State)
        {
            result = result.Replace($"{{{{state.{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }

        return result;
    }

    private static WorkflowResult CreateFailedResult(
        string workflowId,
        string error,
        Stopwatch stopwatch,
        WorkflowContext context
    )
    {
        stopwatch.Stop();
        return new WorkflowResult
        {
            WorkflowId = workflowId,
            Success = false,
            Error = error,
            NodesExecuted = context.ExecutionHistory.Count,
            TotalDurationMs = stopwatch.ElapsedMilliseconds,
            ExecutionHistory = context.ExecutionHistory,
            FinalState = context.State,
        };
    }

    /// <summary>
    /// Safely reads a string value from a config dictionary (compatible with JsonElement and CLR types).
    /// </summary>
    private static string? GetConfigString(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is string s)
        {
            return s;
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
        {
            return je.GetString();
        }

        return value.ToString();
    }

    /// <summary>
    /// Safely reads an integer value from a config dictionary (compatible with JsonElement and CLR types).
    /// </summary>
    private static int? GetConfigInt(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => (int)Math.Clamp(l, int.MinValue, int.MaxValue),
            double d => (int)Math.Clamp(d, int.MinValue, int.MaxValue),
            string s when int.TryParse(s, out var parsed) => parsed,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var i) =>
                i,
            JsonElement je
                when je.ValueKind == JsonValueKind.String
                    && int.TryParse(je.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// Safely reads a condition branch list from a config dictionary (compatible with JsonElement and CLR types).
    /// </summary>
    private static List<Dictionary<string, string>>? GetConfigBranches(
        IReadOnlyDictionary<string, object?> config,
        string key
    )
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        // CLR type: List<Dictionary<string, object?>>
        if (value is List<Dictionary<string, object?>> clrBranches)
        {
            return clrBranches
                .Select(b =>
                    b.Where(kv => kv.Value != null)
                        .ToDictionary(kv => kv.Key, kv => kv.Value!.ToString()!)
                )
                .ToList();
        }

        // Deserialized from JSON: JsonElement (Array)
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var branches = new List<Dictionary<string, string>>();
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var branch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in item.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        branch[prop.Name] = prop.Value.GetString()!;
                    }
                }

                branches.Add(branch);
            }

            return branches;
        }

        return null;
    }
}

/// <summary>
/// Executable workflow (wraps a <see cref="WorkflowDefinition"/>).
/// </summary>
internal class ExecutableWorkflow : IWorkflow
{
    private readonly WorkflowDefinition _definition;
    private readonly WorkflowEngine _engine;

    public ExecutableWorkflow(WorkflowDefinition definition, WorkflowEngine engine)
    {
        _definition = definition;
        _engine = engine;
    }

    public string Id => _definition.Id;
    public string Name => _definition.Name;
    public string? Description => _definition.Description;
    public IReadOnlyList<IWorkflowNode> Nodes => [];
    public string StartNodeId => _definition.StartNodeId;

    public Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default
    )
    {
        return _engine.ExecuteAsync(_definition, context, cancellationToken);
    }
}
