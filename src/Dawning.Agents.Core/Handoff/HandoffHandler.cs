using System.Collections.Concurrent;
using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Handoff;

/// <summary>
/// Handoff 处理器实现 - 管理 Agent 间的任务转交
/// </summary>
public class HandoffHandler : IHandoffHandler
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
        foreach (var agent in agents)
        {
            RegisterAgent(agent);
        }
    }

    /// <inheritdoc />
    public IAgent? GetAgent(string name)
    {
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
        var stopwatch = Stopwatch.StartNew();
        var chain = new List<HandoffRecord>();

        // 添加初始 Handoff 记录
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
        );

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
        var request = HandoffRequest.To(entryAgentName, input);
        return await ExecuteHandoffAsync(request, cancellationToken);
    }

    /// <summary>
    /// 递归执行 Handoff 链
    /// </summary>
    private async Task<HandoffResult> ExecuteHandoffChainAsync(
        string agentName,
        string input,
        List<HandoffRecord> chain,
        HashSet<string> visitedAgents,
        CancellationToken cancellationToken
    )
    {
        // 检查深度限制
        if (chain.Count > _options.MaxHandoffDepth)
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

        // 检查循环
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

        // 获取目标 Agent
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
            // 创建超时令牌
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.TimeoutSeconds)
            );
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token
            );

            // 执行 Agent
            var response = await agent.RunAsync(input, linkedCts.Token);

            // 检查是否需要继续 Handoff
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

                    // 验证 Handoff 目标是否允许
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

                    // 添加 Handoff 记录
                    chain.Add(
                        new HandoffRecord
                        {
                            FromAgent = agentName,
                            ToAgent = handoffRequest.TargetAgentName,
                            Reason = handoffRequest.Reason,
                            Input = handoffRequest.Input,
                        }
                    );

                    // 递归执行下一个 Agent
                    return await ExecuteHandoffChainAsync(
                        handoffRequest.TargetAgentName,
                        handoffRequest.Input,
                        chain,
                        visitedAgents,
                        cancellationToken
                    );
                }
            }

            // 返回最终结果
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
                    // 可以在这里实现回退逻辑
                }
            }

            return HandoffResult.Failed(agentName, ex.Message, chain, TimeSpan.Zero);
        }
    }
}
