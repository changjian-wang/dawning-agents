namespace Dawning.Agents.Core.Orchestration;

using System.Text;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// 并行编排器：同时执行多个 Agent，然后聚合结果
/// </summary>
/// <remarks>
/// 执行流程：
/// <code>
///           ┌─→ Agent A ─→ Result A ─┐
/// Input ────┼─→ Agent B ─→ Result B ─┼──→ Aggregator → Output
///           └─→ Agent C ─→ Result C ─┘
/// </code>
///
/// 使用场景：
/// - 多角度分析：多个专家 Agent 同时分析问题
/// - 冗余执行：多个 Agent 执行相同任务，选择最优结果
/// - 分而治之：将大任务分解给多个 Agent 并行处理
/// </remarks>
public class ParallelOrchestrator : OrchestratorBase
{
    /// <summary>
    /// 自定义结果聚合器
    /// </summary>
    private Func<IReadOnlyList<AgentExecutionRecord>, string>? _customAggregator;

    /// <summary>
    /// 创建并行编排器
    /// </summary>
    public ParallelOrchestrator(
        string name,
        IOptions<OrchestratorOptions>? options = null,
        ILogger<ParallelOrchestrator>? logger = null
    )
        : base(name, options, logger)
    {
        Description = "并行执行多个 Agent，然后聚合结果";
    }

    /// <summary>
    /// 设置自定义结果聚合器
    /// </summary>
    public ParallelOrchestrator WithAggregator(
        Func<IReadOnlyList<AgentExecutionRecord>, string> aggregator
    )
    {
        _customAggregator = aggregator;
        Options.AggregationStrategy = ResultAggregationStrategy.Custom;
        return this;
    }

    /// <inheritdoc />
    protected override async Task<OrchestrationResult> ExecuteOrchestratedAsync(
        OrchestrationContext context,
        CancellationToken cancellationToken
    )
    {
        var input = context.CurrentInput;

        // 使用 SemaphoreSlim 控制并发度
        using var semaphore = new SemaphoreSlim(Options.MaxConcurrency);

        var tasks = _agents.Select(
            async (agent, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ExecuteAgentAsync(agent, input, index, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        );

        AgentExecutionRecord[] results;

        try
        {
            results = await Task.WhenAll(tasks);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(ex, "并行执行 Agent 时发生错误");

            if (!Options.ContinueOnError)
            {
                return OrchestrationResult.Failed(
                    $"并行执行失败: {ex.Message}",
                    context.ExecutionHistory,
                    TimeSpan.Zero
                );
            }

            // 收集已完成的结果
            results = [];
        }

        // 添加到执行历史
        foreach (var record in results)
        {
            context.ExecutionHistory.Add(record);
        }

        // 检查是否所有都失败了
        var successfulResults = results.Where(r => r.Response.Success).ToList();
        if (successfulResults.Count == 0 && results.Length > 0)
        {
            return OrchestrationResult.Failed(
                "所有 Agent 都执行失败",
                context.ExecutionHistory,
                TimeSpan.Zero
            );
        }

        // 聚合结果
        var finalOutput = AggregateResults(results);

        return OrchestrationResult.Successful(
            finalOutput,
            context.ExecutionHistory,
            TimeSpan.Zero,
            new Dictionary<string, object>(context.Metadata)
        );
    }

    /// <summary>
    /// 根据策略聚合结果
    /// </summary>
    private string AggregateResults(AgentExecutionRecord[] results)
    {
        if (results.Length == 0)
        {
            return string.Empty;
        }

        return Options.AggregationStrategy switch
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
        // 简单投票：选择出现次数最多的答案
        var answers = results
            .Where(r => r.Response.Success && !string.IsNullOrWhiteSpace(r.Response.FinalAnswer))
            .GroupBy(r => r.Response.FinalAnswer!.Trim())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return answers?.Key ?? string.Empty;
    }
}
