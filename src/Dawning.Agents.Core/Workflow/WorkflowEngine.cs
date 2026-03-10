using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Abstractions.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Workflow;

/// <summary>
/// 工作流引擎
/// </summary>
public class WorkflowEngine : IWorkflowEngine
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
            throw new InvalidOperationException($"工作流定义无效: {errors}");
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
            "开始执行工作流 {WorkflowId}: {WorkflowName}",
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
                        $"工作流执行超过最大步骤数 ({maxExecutionSteps})，可能存在循环",
                        stopwatch,
                        context
                    );
                }

                if (!nodeMap.TryGetValue(currentNodeId, out var nodeDefinition))
                {
                    return CreateFailedResult(
                        definition.Id,
                        $"找不到节点: {currentNodeId}",
                        stopwatch,
                        context
                    );
                }

                // 记录执行步骤开始
                var step = new WorkflowExecutionStep
                {
                    NodeId = nodeDefinition.Id,
                    NodeName = nodeDefinition.Name,
                    NodeType = nodeDefinition.Type,
                    StartedAt = DateTime.UtcNow,
                };

                _logger.LogDebug(
                    "执行节点 {NodeId}: {NodeName} ({NodeType})",
                    nodeDefinition.Id,
                    nodeDefinition.Name,
                    nodeDefinition.Type
                );

                // 执行节点
                var result = await ExecuteNodeAsync(nodeDefinition, context, cancellationToken)
                    .ConfigureAwait(false);
                context.AddNodeResult(nodeDefinition.Id, result);

                // 更新执行步骤
                context.AddExecutionStep(
                    step with
                    {
                        CompletedAt = DateTime.UtcNow,
                        Success = result.Success,
                    }
                );

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "节点 {NodeId} 执行失败: {Error}",
                        nodeDefinition.Id,
                        result.Error
                    );
                    return CreateFailedResult(
                        definition.Id,
                        result.Error ?? "节点执行失败",
                        stopwatch,
                        context
                    );
                }

                // 决定下一个节点
                currentNodeId = DetermineNextNode(nodeDefinition, result, definition.Edges);

                // 结束节点
                if (nodeDefinition.Type == WorkflowNodeType.End)
                {
                    break;
                }
            }

            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                return CreateFailedResult(definition.Id, "工作流执行被取消", stopwatch, context);
            }

            var finalOutput = context.GetLastResult()?.Output;

            _logger.LogInformation(
                "工作流 {WorkflowId} 执行完成，耗时 {Duration}ms，执行了 {NodeCount} 个节点",
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
        catch (OperationCanceledException)
        {
            _logger.LogWarning("工作流 {WorkflowId} 被取消", definition.Id);
            return CreateFailedResult(definition.Id, "工作流执行被取消", stopwatch, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工作流 {WorkflowId} 执行异常", definition.Id);
            return CreateFailedResult(definition.Id, ex.Message, stopwatch, context);
        }
    }

    /// <inheritdoc />
    public WorkflowValidationResult Validate(WorkflowDefinition definition)
    {
        var errors = new List<WorkflowValidationError>();
        var warnings = new List<WorkflowValidationWarning>();
        var nodeIds = definition.Nodes.Select(n => n.Id).ToHashSet();

        // 检查起始节点
        if (!nodeIds.Contains(definition.StartNodeId))
        {
            errors.Add(
                new WorkflowValidationError
                {
                    Code = "INVALID_START_NODE",
                    Message = $"起始节点 '{definition.StartNodeId}' 不存在",
                }
            );
        }

        // 检查边的有效性
        foreach (var edge in definition.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId))
            {
                errors.Add(
                    new WorkflowValidationError
                    {
                        Code = "INVALID_EDGE_SOURCE",
                        Message = $"边的源节点 '{edge.FromNodeId}' 不存在",
                    }
                );
            }

            if (!nodeIds.Contains(edge.ToNodeId))
            {
                errors.Add(
                    new WorkflowValidationError
                    {
                        Code = "INVALID_EDGE_TARGET",
                        Message = $"边的目标节点 '{edge.ToNodeId}' 不存在",
                    }
                );
            }
        }

        // 检查结束节点
        var hasEndNode = definition.Nodes.Any(n => n.Type == WorkflowNodeType.End);
        if (!hasEndNode)
        {
            warnings.Add(
                new WorkflowValidationWarning
                {
                    Code = "NO_END_NODE",
                    Message = "工作流没有结束节点，可能会导致无限执行",
                }
            );
        }

        // 检查孤立节点
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
                        Message = $"节点 '{node.Id}' 未被任何边引用",
                        NodeId = node.Id,
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
                    $"不支持的节点类型: {nodeDefinition.Type}"
                ),
            };

            stopwatch.Stop();
            return result with { DurationMs = stopwatch.ElapsedMilliseconds };
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
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Agent 节点缺少 agentName 配置");
        }

        var agentName = agentNameObj?.ToString();
        if (string.IsNullOrEmpty(agentName))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "Agent 名称不能为空");
        }

        // 从 DI 获取 Agent（按名称匹配）
        var agents = _serviceProvider.GetServices<IAgent>();
        var agent = agents.FirstOrDefault(a =>
            string.Equals(a.Name, agentName, StringComparison.OrdinalIgnoreCase)
        );
        if (agent == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, $"找不到 Agent: {agentName}");
        }

        // 构建输入
        var input = context.GetLastResult()?.Output ?? context.Input;
        if (
            config.TryGetValue("inputTemplate", out var templateObj)
            && templateObj is string template
        )
        {
            input = ReplaceVariables(template, context);
        }

        // 执行 Agent
        var response = await agent.RunAsync(input, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, response.Error ?? "Agent 执行失败");
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
        if (config == null || !config.TryGetValue("toolName", out var toolNameObj))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "工具节点缺少 toolName 配置");
        }

        var toolName = toolNameObj?.ToString();
        if (string.IsNullOrEmpty(toolName))
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "工具名称不能为空");
        }

        if (_toolRegistry == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, "未配置工具注册表");
        }

        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, $"找不到工具: {toolName}");
        }

        // 构建输入
        var input = context.GetLastResult()?.Output ?? context.Input;
        if (
            config.TryGetValue("inputTemplate", out var templateObj)
            && templateObj is string template
        )
        {
            input = ReplaceVariables(template, context);
        }

        // 执行工具
        var toolResult = await tool.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        if (toolResult.Success)
        {
            return NodeExecutionResult.Ok(nodeDefinition.Id, toolResult.Output);
        }
        else
        {
            return NodeExecutionResult.Fail(nodeDefinition.Id, toolResult.Error ?? "工具执行失败");
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
            return NodeExecutionResult.Fail(nodeDefinition.Id, "条件节点缺少配置");
        }

        // 获取输入值
        var inputValue = context.GetLastResult()?.Output ?? context.Input;
        if (
            config.TryGetValue("inputSource", out var inputSourceObj)
            && inputSourceObj is string inputSource
        )
        {
            inputValue = context.GetState<string>(inputSource) ?? inputValue;
        }

        // 检查分支条件
        if (
            config.TryGetValue("branches", out var branchesObj)
            && branchesObj is List<Dictionary<string, object?>> branches
        )
        {
            foreach (var branch in branches)
            {
                if (
                    branch.TryGetValue("condition", out var conditionObj)
                    && conditionObj is string condition
                )
                {
                    if (EvaluateCondition(condition, inputValue, context))
                    {
                        if (
                            branch.TryGetValue("targetNodeId", out var targetObj)
                            && targetObj is string targetNodeId
                        )
                        {
                            return NodeExecutionResult.Branch(nodeDefinition.Id, targetNodeId);
                        }
                    }
                }
            }
        }

        // 默认分支
        if (
            config.TryGetValue("defaultBranchNodeId", out var defaultObj)
            && defaultObj is string defaultNodeId
        )
        {
            return NodeExecutionResult.Branch(nodeDefinition.Id, defaultNodeId);
        }

        return NodeExecutionResult.Fail(nodeDefinition.Id, "条件节点没有匹配的分支");
    }

    private async Task<NodeExecutionResult> ExecuteDelayNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        CancellationToken cancellationToken
    )
    {
        var config = nodeDefinition.Config;
        var delayMs = 1000;

        if (config?.TryGetValue("delayMs", out var delayObj) == true)
        {
            delayMs = Math.Max(0, Convert.ToInt32(delayObj));
        }

        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        return NodeExecutionResult.Ok(nodeDefinition.Id);
    }

    private async Task<NodeExecutionResult> ExecuteParallelNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        // 并行执行简化实现 - 返回成功
        await Task.CompletedTask.ConfigureAwait(false);
        return NodeExecutionResult.Ok(nodeDefinition.Id, "并行节点执行完成");
    }

    private async Task<NodeExecutionResult> ExecuteLoopNodeAsync(
        WorkflowNodeDefinition nodeDefinition,
        WorkflowContext context,
        CancellationToken cancellationToken
    )
    {
        var config = nodeDefinition.Config;
        var maxIterations = 10;

        if (config?.TryGetValue("maxIterations", out var maxObj) == true)
        {
            maxIterations = Convert.ToInt32(maxObj);
        }

        // 简化实现 - 只是标记循环次数
        context.SetState($"{nodeDefinition.Id}_iterations", maxIterations);
        await Task.CompletedTask.ConfigureAwait(false);
        return NodeExecutionResult.Ok(
            nodeDefinition.Id,
            $"循环节点完成，最大迭代 {maxIterations} 次"
        );
    }

    private string? DetermineNextNode(
        WorkflowNodeDefinition currentNode,
        NodeExecutionResult result,
        List<WorkflowEdgeDefinition> edges
    )
    {
        // 如果节点指定了下一个节点（条件分支）
        if (!string.IsNullOrEmpty(result.NextNodeId))
        {
            return result.NextNodeId;
        }

        // 查找边
        var outgoingEdge = edges.FirstOrDefault(e => e.FromNodeId == currentNode.Id);
        return outgoingEdge?.ToNodeId;
    }

    private bool EvaluateCondition(string condition, string inputValue, WorkflowContext context)
    {
        // 简单的条件评估
        // 支持: contains:xxx, equals:xxx, startsWith:xxx, endsWith:xxx
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

        // 替换 {{input}}
        result = result.Replace("{{input}}", context.Input);

        // 替换 {{lastOutput}}
        result = result.Replace("{{lastOutput}}", context.GetLastResult()?.Output ?? "");

        // 替换 {{state.xxx}}
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
}

/// <summary>
/// 可执行工作流（包装 WorkflowDefinition）
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
