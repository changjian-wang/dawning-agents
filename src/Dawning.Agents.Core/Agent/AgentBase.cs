using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// Agent 基类，提供通用的执行框架
/// </summary>
/// <remarks>
/// <para>实现了 Agent 核心循环：Observe → Think → Act</para>
/// <para>子类只需实现 <see cref="ExecuteStepAsync"/> 和 <see cref="ExtractFinalAnswer"/> 方法</para>
/// </remarks>
public abstract class AgentBase : IAgent
{
    /// <summary>
    /// LLM 提供者，用于与语言模型交互
    /// </summary>
    protected readonly ILLMProvider LLMProvider;

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Agent 配置选项
    /// </summary>
    protected readonly AgentOptions Options;

    /// <summary>
    /// 对话记忆（可选），用于跨会话保持上下文
    /// </summary>
    protected readonly IConversationMemory? Memory;

    /// <summary>
    /// Agent 名称
    /// </summary>
    public virtual string Name => Options.Name;

    /// <summary>
    /// Agent 系统指令
    /// </summary>
    public virtual string Instructions => Options.Instructions;

    /// <summary>
    /// 初始化 Agent 基类
    /// </summary>
    /// <param name="llmProvider">LLM 提供者</param>
    /// <param name="options">Agent 配置选项</param>
    /// <param name="memory">对话记忆（可选）</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <exception cref="ArgumentNullException">当 llmProvider 或 options 为 null 时抛出</exception>
    protected AgentBase(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IConversationMemory? memory = null,
        ILogger? logger = null
    )
    {
        LLMProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        Memory = memory;
        Logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// 执行 Agent 任务
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 响应</returns>
    public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        var context = new AgentContext { UserInput = input, MaxSteps = Options.MaxSteps };
        return RunAsync(context, cancellationToken);
    }

    /// <summary>
    /// 使用指定上下文执行 Agent 任务
    /// </summary>
    /// <param name="context">执行上下文，包含用户输入和历史步骤</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 响应</returns>
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("Agent {AgentName} 开始执行任务: {Input}", Name, context.UserInput);

        try
        {
            while (context.Steps.Count < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepNumber = context.Steps.Count + 1;
                Logger.LogDebug("执行步骤 {StepNumber}/{MaxSteps}", stepNumber, context.MaxSteps);

                // 执行单步
                var step = await ExecuteStepAsync(context, stepNumber, cancellationToken);
                context.Steps.Add(step);

                // 检查是否有最终答案
                var finalAnswer = ExtractFinalAnswer(step);
                if (finalAnswer != null)
                {
                    stopwatch.Stop();
                    Logger.LogInformation(
                        "Agent {AgentName} 完成任务，共 {StepCount} 步",
                        Name,
                        context.Steps.Count
                    );

                    // 保存对话到记忆
                    await SaveToMemoryAsync(context.UserInput, finalAnswer, cancellationToken);

                    return AgentResponse.Successful(finalAnswer, context.Steps, stopwatch.Elapsed);
                }
            }

            // 超过最大步数
            stopwatch.Stop();
            Logger.LogWarning("Agent {AgentName} 超过最大步数 {MaxSteps}", Name, context.MaxSteps);
            return AgentResponse.Failed(
                $"Exceeded maximum steps ({context.MaxSteps})",
                context.Steps,
                stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Logger.LogWarning("Agent {AgentName} 任务被取消", Name);
            return AgentResponse.Failed("Operation cancelled", context.Steps, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Agent {AgentName} 执行出错", Name);
            return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// 执行单个步骤，由子类实现具体的推理和行动逻辑
    /// </summary>
    /// <param name="context">当前执行上下文，包含用户输入和历史步骤</param>
    /// <param name="stepNumber">当前步骤编号（从 1 开始）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果，包含思考、动作和观察</returns>
    protected abstract Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// 从步骤中提取最终答案
    /// </summary>
    /// <param name="step">当前执行步骤</param>
    /// <returns>最终答案字符串，如果该步骤不包含最终答案则返回 null</returns>
    /// <remarks>
    /// 当返回非 null 值时，Agent 循环将终止并返回成功响应
    /// </remarks>
    protected abstract string? ExtractFinalAnswer(AgentStep step);

    /// <summary>
    /// 保存对话到记忆（如果已配置）
    /// </summary>
    /// <param name="userInput">用户输入</param>
    /// <param name="assistantResponse">Agent 响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task SaveToMemoryAsync(
        string userInput,
        string assistantResponse,
        CancellationToken cancellationToken
    )
    {
        if (Memory == null)
        {
            return;
        }

        try
        {
            await Memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = userInput },
                cancellationToken
            );
            await Memory.AddMessageAsync(
                new ConversationMessage { Role = "assistant", Content = assistantResponse },
                cancellationToken
            );
            Logger.LogDebug("对话已保存到记忆，当前消息数: {Count}", Memory.MessageCount);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "保存对话到记忆时出错");
        }
    }
}
