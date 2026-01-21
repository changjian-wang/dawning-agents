namespace Dawning.Agents.Core.Orchestration;

using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// 编排器基类，提供通用功能
/// </summary>
public abstract class OrchestratorBase : IOrchestrator
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// 编排器配置
    /// </summary>
    protected readonly OrchestratorOptions Options;

    /// <summary>
    /// Agent 列表
    /// </summary>
    protected readonly List<IAgent> _agents = [];

    /// <summary>
    /// 创建编排器基类
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
    public IReadOnlyList<IAgent> Agents => _agents.AsReadOnly();

    /// <summary>
    /// 添加 Agent 到编排器
    /// </summary>
    public OrchestratorBase AddAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        _agents.Add(agent);
        Logger.LogDebug("Agent {AgentName} 已添加到编排器 {OrchestratorName}", agent.Name, Name);
        return this;
    }

    /// <summary>
    /// 添加多个 Agent
    /// </summary>
    public OrchestratorBase AddAgents(IEnumerable<IAgent> agents)
    {
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
        var context = new OrchestrationContext { UserInput = input, CurrentInput = input };

        return await RunAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrchestrationResult> RunAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_agents.Count == 0)
        {
            return OrchestrationResult.Failed("编排器中没有 Agent", [], TimeSpan.Zero);
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
                "编排器 {OrchestratorName} 开始执行，共 {AgentCount} 个 Agent",
                Name,
                _agents.Count
            );

            var result = await ExecuteOrchestratedAsync(context, linkedCts.Token);

            stopwatch.Stop();

            Logger.LogInformation(
                "编排器 {OrchestratorName} 执行完成，耗时 {Duration}ms，结果: {Success}",
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
            Logger.LogWarning("编排器 {OrchestratorName} 执行超时", Name);

            return OrchestrationResult.Failed(
                $"编排执行超时（{Options.TimeoutSeconds}秒）",
                context.ExecutionHistory,
                stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "编排器 {OrchestratorName} 执行出错", Name);

            return OrchestrationResult.Failed(
                ex.Message,
                context.ExecutionHistory,
                stopwatch.Elapsed
            );
        }
    }

    /// <summary>
    /// 执行具体的编排逻辑（由子类实现）
    /// </summary>
    protected abstract Task<OrchestrationResult> ExecuteOrchestratedAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// 执行单个 Agent
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

        Logger.LogDebug("开始执行 Agent {AgentName}，顺序: {Order}", agent.Name, executionOrder);

        AgentResponse response;
        try
        {
            response = await agent.RunAsync(input, linkedCts.Token);
        }
        catch (OperationCanceledException) when (agentCts.IsCancellationRequested)
        {
            response = AgentResponse.Failed(
                $"Agent {agent.Name} 执行超时（{Options.AgentTimeoutSeconds}秒）",
                [],
                TimeSpan.FromSeconds(Options.AgentTimeoutSeconds)
            );
        }
        catch (Exception ex)
        {
            response = AgentResponse.Failed(ex.Message, [], TimeSpan.Zero);
        }

        var endTime = DateTimeOffset.UtcNow;

        Logger.LogDebug(
            "Agent {AgentName} 执行完成，结果: {Success}",
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
