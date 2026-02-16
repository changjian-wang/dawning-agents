using System.Diagnostics;
using System.Text;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// 基于 Native Function Calling 的 Agent 实现
/// </summary>
/// <remarks>
/// <para>使用 LLM 原生 Function Calling（ToolCalls）代替文本解析</para>
/// <para>流程：构建消息 → ChatAsync → 检测 ToolCalls → 执行工具 → 回传结果 → 循环</para>
/// <para>相比 ReActAgent（基于正则解析），此方式更可靠、准确率更高</para>
/// </remarks>
public class FunctionCallingAgent : AgentBase
{
    private readonly IToolRegistry _toolRegistry;

    /// <summary>
    /// 初始化 Function Calling Agent
    /// </summary>
    /// <param name="llmProvider">LLM 提供者</param>
    /// <param name="options">Agent 配置选项</param>
    /// <param name="toolRegistry">工具注册表（必须提供）</param>
    /// <param name="memory">对话记忆（可选）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public FunctionCallingAgent(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IToolRegistry toolRegistry,
        IConversationMemory? memory = null,
        ILogger<FunctionCallingAgent>? logger = null
    )
        : base(llmProvider, options, memory, logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
    }

    /// <summary>
    /// 使用 Function Calling 模式执行 Agent 任务
    /// </summary>
    /// <remarks>
    /// 重写基类的 RunAsync 以实现完整的 Function Calling 循环：
    /// <list type="number">
    /// <item>构建消息列表（含工具定义）</item>
    /// <item>调用 LLM</item>
    /// <item>如果响应包含 ToolCalls：执行工具 → 将结果回传 → 继续循环</item>
    /// <item>如果响应不含 ToolCalls：返回最终答案</item>
    /// </list>
    /// </remarks>
    public new async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation(
            "FunctionCallingAgent {AgentName} 开始执行任务: {Input}",
            Name,
            context.UserInput
        );

        try
        {
            // 构建工具定义
            var toolDefinitions = BuildToolDefinitions();

            // 构建消息历史
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(Instructions))
            {
                messages.Add(ChatMessage.System(Instructions));
            }

            // 从 Memory 加载历史
            if (Memory != null)
            {
                var history = await Memory.GetContextAsync(cancellationToken: cancellationToken);
                messages.AddRange(history);
            }

            messages.Add(ChatMessage.User(context.UserInput));

            // Function Calling 循环
            var step = 0;
            while (step < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                step++;

                Logger.LogDebug(
                    "Function Calling 步骤 {Step}/{MaxSteps}",
                    step,
                    context.MaxSteps
                );

                var completionOptions = new ChatCompletionOptions
                {
                    MaxTokens = Options.MaxTokens,
                    Tools = toolDefinitions.Count > 0 ? toolDefinitions : null,
                    ToolChoice = toolDefinitions.Count > 0 ? ToolChoiceMode.Auto : null,
                };

                var response = await LLMProvider.ChatAsync(
                    messages,
                    completionOptions,
                    cancellationToken
                );

                if (response.HasToolCalls)
                {
                    // LLM 请求调用工具
                    Logger.LogDebug(
                        "收到 {Count} 个工具调用请求",
                        response.ToolCalls!.Count
                    );

                    // 添加 assistant 消息（含 tool calls）
                    messages.Add(
                        ChatMessage.AssistantWithToolCalls(
                            response.ToolCalls!,
                            response.Content
                        )
                    );

                    // 执行每个工具调用并添加结果消息
                    var toolResultSummary = new StringBuilder();
                    foreach (var toolCall in response.ToolCalls!)
                    {
                        var toolResult = await ExecuteToolCallAsync(
                            toolCall,
                            cancellationToken
                        );

                        // 添加 tool result 消息
                        messages.Add(
                            ChatMessage.ToolResult(toolCall.Id, toolResult)
                        );

                        toolResultSummary.AppendLine(
                            $"[{toolCall.FunctionName}]: {toolResult}"
                        );
                    }

                    // 记录步骤
                    var toolNames = string.Join(
                        ", ",
                        response.ToolCalls!.Select(tc => tc.FunctionName)
                    );
                    context.Steps.Add(new AgentStep
                    {
                        StepNumber = step,
                        RawOutput = response.Content,
                        Thought = $"调用工具: {toolNames}",
                        Action = toolNames,
                        ActionInput = string.Join(
                            "; ",
                            response.ToolCalls!.Select(tc =>
                                $"{tc.FunctionName}({tc.Arguments})"
                            )
                        ),
                        Observation = toolResultSummary.ToString().TrimEnd(),
                    });
                }
                else
                {
                    // LLM 返回最终答案（无工具调用）
                    var finalAnswer = response.Content;

                    context.Steps.Add(new AgentStep
                    {
                        StepNumber = step,
                        RawOutput = finalAnswer,
                        Thought = "生成最终答案",
                    });

                    stopwatch.Stop();
                    Logger.LogInformation(
                        "FunctionCallingAgent {AgentName} 完成任务，共 {Steps} 步",
                        Name,
                        context.Steps.Count
                    );

                    // 保存到 Memory
                    if (Memory != null)
                    {
                        await SaveToMemoryInternalAsync(
                            context.UserInput,
                            finalAnswer,
                            cancellationToken
                        );
                    }

                    return AgentResponse.Successful(
                        finalAnswer,
                        context.Steps,
                        stopwatch.Elapsed
                    );
                }
            }

            // 超过最大步数
            stopwatch.Stop();
            Logger.LogWarning(
                "FunctionCallingAgent {AgentName} 超过最大步数 {MaxSteps}",
                Name,
                context.MaxSteps
            );
            return AgentResponse.Failed(
                $"Exceeded maximum steps ({context.MaxSteps})",
                context.Steps,
                stopwatch.Elapsed
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Logger.LogWarning("FunctionCallingAgent {AgentName} 任务被取消", Name);
            return AgentResponse.Failed(
                "Operation cancelled",
                context.Steps,
                stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "FunctionCallingAgent {AgentName} 执行出错", Name);
            return AgentResponse.Failed(
                ex.Message,
                context.Steps,
                stopwatch.Elapsed,
                ex
            );
        }
    }

    /// <summary>
    /// 使用 Function Calling 模式执行 Agent 任务
    /// </summary>
    public new Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input, MaxSteps = Options.MaxSteps };
        return RunAsync(context, cancellationToken);
    }

    /// <summary>
    /// 执行单个工具调用
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken
    )
    {
        var tool = _toolRegistry.GetTool(toolCall.FunctionName);
        if (tool == null)
        {
            var errorMsg = $"Tool '{toolCall.FunctionName}' not found";
            Logger.LogWarning(errorMsg);
            return errorMsg;
        }

        Logger.LogDebug(
            "执行工具 {ToolName}，参数: {Args}",
            toolCall.FunctionName,
            toolCall.Arguments
        );

        try
        {
            var result = await tool.ExecuteAsync(
                toolCall.Arguments ?? "{}",
                cancellationToken
            );

            return result.Success
                ? result.Output
                : $"Tool error: {result.Error}";
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "工具 {ToolName} 执行失败",
                toolCall.FunctionName
            );
            return $"Tool execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// 从 IToolRegistry 构建 ToolDefinition 列表
    /// </summary>
    private List<ToolDefinition> BuildToolDefinitions()
    {
        var allTools = _toolRegistry.GetAllTools();
        return allTools
            .Select(t => new ToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                ParametersSchema = t.ParametersSchema,
            })
            .ToList();
    }

    /// <summary>
    /// 保存到对话 Memory
    /// </summary>
    private async Task SaveToMemoryInternalAsync(
        string userInput,
        string assistantResponse,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Memory!.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = userInput },
                cancellationToken
            );
            await Memory.AddMessageAsync(
                new ConversationMessage { Role = "assistant", Content = assistantResponse },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "保存对话到记忆时出错");
        }
    }

    // AgentBase 抽象方法的最小实现（FunctionCallingAgent 重写了 RunAsync，不使用这些方法）

    /// <inheritdoc/>
    protected override Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException(
            "FunctionCallingAgent 使用 Native Function Calling 循环，不走 ExecuteStepAsync 路径"
        );
    }

    /// <inheritdoc/>
    protected override string? ExtractFinalAnswer(AgentStep step)
    {
        throw new NotSupportedException(
            "FunctionCallingAgent 使用 Native Function Calling 循环，不走 ExtractFinalAnswer 路径"
        );
    }
}
