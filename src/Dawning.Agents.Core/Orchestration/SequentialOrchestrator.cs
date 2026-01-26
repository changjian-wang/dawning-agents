namespace Dawning.Agents.Core.Orchestration;

using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Orchestration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// 顺序编排器：按顺序执行 Agent，前一个的输出作为后一个的输入
/// </summary>
/// <remarks>
/// 执行流程：
/// <code>
/// Input → Agent A → Agent B → Agent C → Output
///         ↓          ↓          ↓
///       Result A   Result B   Result C (Final)
/// </code>
///
/// 使用场景：
/// - 流水线处理：翻译 → 润色 → 格式化
/// - 逐步推理：分析 → 计划 → 执行
/// - 数据转换：提取 → 清洗 → 存储
/// </remarks>
public sealed class SequentialOrchestrator : OrchestratorBase
{
    /// <summary>
    /// 输入转换器（在传递给下一个 Agent 前转换输出）
    /// </summary>
    private Func<AgentExecutionRecord, string>? _inputTransformer;

    /// <summary>
    /// 创建顺序编排器
    /// </summary>
    public SequentialOrchestrator(
        string name,
        IOptions<OrchestratorOptions>? options = null,
        ILogger<SequentialOrchestrator>? logger = null
    )
        : base(name, options, logger)
    {
        Description = "顺序执行多个 Agent，将前一个的输出作为后一个的输入";
    }

    /// <summary>
    /// 设置输入转换器
    /// </summary>
    /// <param name="transformer">转换函数，将 Agent 执行记录转换为下一个 Agent 的输入</param>
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

        for (var i = 0; i < _agents.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.ShouldStop)
            {
                Logger.LogInformation(
                    "编排在 Agent {Index}/{Total} 处停止，原因: {Reason}",
                    i + 1,
                    _agents.Count,
                    context.StopReason
                );
                break;
            }

            var agent = _agents[i];
            var record = await ExecuteAgentAsync(agent, currentInput, i, cancellationToken);
            context.ExecutionHistory.Add(record);

            if (!record.Response.Success)
            {
                if (!Options.ContinueOnError)
                {
                    return OrchestrationResult.Failed(
                        $"Agent {agent.Name} 执行失败: {record.Response.Error}",
                        context.ExecutionHistory,
                        TimeSpan.Zero
                    );
                }

                Logger.LogWarning("Agent {AgentName} 执行失败，但继续执行下一个 Agent", agent.Name);
            }
            else
            {
                // 转换输出作为下一个 Agent 的输入
                currentInput =
                    _inputTransformer != null
                        ? _inputTransformer(record)
                        : record.Response.FinalAnswer ?? currentInput;

                context.CurrentInput = currentInput;
            }
        }

        // 获取最终结果
        var finalRecord = context.ExecutionHistory.LastOrDefault();
        var finalOutput = finalRecord?.Response.FinalAnswer ?? string.Empty;

        return OrchestrationResult.Successful(
            finalOutput,
            context.ExecutionHistory,
            TimeSpan.Zero,
            new Dictionary<string, object>(context.Metadata)
        );
    }
}
