using System.Diagnostics;
using System.Text;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools.Core;
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
/// <para>当注入 IToolSession 时，支持动态工具创建（create_tool）和 session/user/global 工具加载</para>
/// </remarks>
public class FunctionCallingAgent : AgentBase
{
    private readonly IToolReader _toolRegistry;
    private readonly IToolSession? _toolSession;
    private readonly CreateToolTool? _createToolTool;

    /// <summary>
    /// 初始化 Function Calling Agent
    /// </summary>
    /// <param name="llmProvider">LLM 提供者</param>
    /// <param name="options">Agent 配置选项</param>
    /// <param name="toolRegistry">工具只读查询（必须提供）</param>
    /// <param name="memory">对话记忆（可选）</param>
    /// <param name="toolSession">工具会话（可选，启用动态工具创建）</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <param name="usageTracker">工具使用追踪器（可选）</param>
    public FunctionCallingAgent(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IToolReader toolRegistry,
        IConversationMemory? memory = null,
        IToolSession? toolSession = null,
        ILogger<FunctionCallingAgent>? logger = null,
        IToolUsageTracker? usageTracker = null
    )
        : base(llmProvider, options, memory, logger, usageTracker)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolSession = toolSession;

        // If session is available, create the create_tool tool
        if (_toolSession != null)
        {
            _createToolTool = new CreateToolTool(_toolSession);
        }
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
    public override async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var costTracker = CreateCostTracker();
        Logger.LogInformation(
            "FunctionCallingAgent {AgentName} 开始执行任务，输入长度: {InputLength}",
            Name,
            context.UserInput.Length
        );

        try
        {
            // 构建消息历史
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(Instructions))
            {
                messages.Add(ChatMessage.System(Instructions));
            }

            // 从 Memory 加载历史
            if (Memory != null)
            {
                var history = await Memory
                    .GetContextAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                messages.AddRange(history);
            }

            messages.Add(ChatMessage.User(context.UserInput));

            // Function Calling 循环
            var step = 0;
            // 消息数量上限：每步添加 1 个 assistant + N 个 tool result，限制总消息数防止 OOM
            var maxMessages = context.MaxSteps * 10 + messages.Count;
            while (step < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (messages.Count > maxMessages)
                {
                    Logger.LogWarning(
                        "FunctionCallingAgent {AgentName} 消息数量超限 {Count}/{Max}",
                        Name,
                        messages.Count,
                        maxMessages
                    );

                    context.AddStep(
                        new AgentStep
                        {
                            StepNumber = step + 1,
                            Thought = "消息数量超限，终止循环",
                            Action = "Overflow",
                            Observation = $"消息数量 {messages.Count} 超过上限 {maxMessages}",
                        }
                    );
                    break;
                }

                step++;

                // Rebuild tool definitions each iteration — session tools may have changed
                // (e.g. create_tool was called in a previous step)
                var toolDefinitions = BuildToolDefinitions();

                Logger.LogDebug("Function Calling 步骤 {Step}/{MaxSteps}", step, context.MaxSteps);

                var completionOptions = new ChatCompletionOptions
                {
                    MaxTokens = Options.MaxTokens,
                    Tools = toolDefinitions.Count > 0 ? toolDefinitions : null,
                    ToolChoice = toolDefinitions.Count > 0 ? ToolChoiceMode.Auto : null,
                };

                var response = await LLMProvider
                    .ChatAsync(messages, completionOptions, cancellationToken)
                    .ConfigureAwait(false);

                var stepCost = EstimateStepCost(response);
                costTracker?.Add(stepCost);

                if (response.HasToolCalls)
                {
                    // LLM 请求调用工具
                    Logger.LogDebug("收到 {Count} 个工具调用请求", response.ToolCalls!.Count);

                    // 添加 assistant 消息（含 tool calls）
                    messages.Add(
                        ChatMessage.AssistantWithToolCalls(response.ToolCalls!, response.Content)
                    );

                    // 执行每个工具调用并添加结果消息
                    var toolResultSummary = new StringBuilder();
                    foreach (var toolCall in response.ToolCalls!)
                    {
                        var toolResult = await ExecuteToolCallAsync(toolCall, cancellationToken)
                            .ConfigureAwait(false);

                        // 添加 tool result 消息
                        messages.Add(ChatMessage.ToolResult(toolCall.Id, toolResult));

                        toolResultSummary.AppendLine($"[{toolCall.FunctionName}]: {toolResult}");
                    }

                    // 记录步骤
                    var toolNames = string.Join(
                        ", ",
                        response.ToolCalls!.Select(tc => tc.FunctionName)
                    );
                    context.AddStep(
                        new AgentStep
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
                            Cost = stepCost,
                        }
                    );
                }
                else
                {
                    // LLM 返回最终答案（无工具调用）
                    var finalAnswer = response.Content ?? string.Empty;

                    context.AddStep(
                        new AgentStep
                        {
                            StepNumber = step,
                            RawOutput = finalAnswer,
                            Thought = "生成最终答案",
                            Cost = stepCost,
                        }
                    );

                    stopwatch.Stop();
                    Logger.LogInformation(
                        "FunctionCallingAgent {AgentName} 完成任务，共 {Steps} 步",
                        Name,
                        context.Steps.Count
                    );

                    // 保存到 Memory
                    await SaveToMemoryAsync(context.UserInput, finalAnswer, cancellationToken)
                        .ConfigureAwait(false);

                    return AgentResponse.Successful(finalAnswer, context.Steps, stopwatch.Elapsed);
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
        catch (BudgetExceededException ex)
        {
            stopwatch.Stop();
            Logger.LogWarning(
                "FunctionCallingAgent {AgentName} 成本超出预算: {TotalCost:F4} > {Budget:F4}",
                Name,
                ex.TotalCost,
                ex.Budget
            );
            return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed, ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogWarning(ex, "FunctionCallingAgent {AgentName} 任务被取消", Name);
            return AgentResponse.Failed(
                "Operation cancelled",
                context.Steps,
                stopwatch.Elapsed,
                ex
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "FunctionCallingAgent {AgentName} 执行出错", Name);
            return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed, ex);
        }
    }

    /// <summary>
    /// 执行单个工具调用
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken
    )
    {
        var tool = ResolveTool(toolCall.FunctionName);
        if (tool == null)
        {
            Logger.LogWarning("Tool '{ToolName}' not found", toolCall.FunctionName);
            return $"Tool '{toolCall.FunctionName}' not found";
        }

        Logger.LogDebug(
            "执行工具 {ToolName}，参数: {Args}",
            toolCall.FunctionName,
            toolCall.Arguments
        );

        try
        {
            var result = await tool.ExecuteAsync(toolCall.Arguments ?? "{}", cancellationToken)
                .ConfigureAwait(false);

            await RecordToolCallUsageAsync(
                    toolCall.FunctionName,
                    result.Success,
                    result.Error,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return result.Success ? result.Output : $"Tool error: {result.Error}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "工具 {ToolName} 执行失败", toolCall.FunctionName);
            await RecordToolCallUsageAsync(
                    toolCall.FunctionName,
                    false,
                    ex.Message,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return $"Tool execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// 记录工具调用到追踪器
    /// </summary>
    private async Task RecordToolCallUsageAsync(
        string toolName,
        bool success,
        string? error,
        CancellationToken cancellationToken
    )
    {
        if (UsageTracker == null)
        {
            return;
        }

        try
        {
            var record = new ToolUsageRecord
            {
                ToolName = toolName,
                Success = success,
                Duration = TimeSpan.Zero,
                ErrorMessage = success ? null : error,
            };
            await UsageTracker.RecordUsageAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record tool usage for '{ToolName}'", toolName);
        }
    }

    /// <summary>
    /// 解析工具：Registry → create_tool → Session 工具
    /// </summary>
    private ITool? ResolveTool(string name)
    {
        // 1. Registry (core + user-registered tools)
        var tool = _toolRegistry.GetTool(name);
        if (tool != null)
        {
            return tool;
        }

        // 2. create_tool (built-in, not in registry)
        if (
            _createToolTool != null
            && string.Equals(name, _createToolTool.Name, StringComparison.OrdinalIgnoreCase)
        )
        {
            return _createToolTool;
        }

        // 3. Session tools (ephemeral tools created at runtime)
        if (_toolSession != null)
        {
            return _toolSession
                .GetSessionTools()
                .FirstOrDefault(t =>
                    string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                );
        }

        return null;
    }

    /// <summary>
    /// 从 IToolRegistry + IToolSession 构建 ToolDefinition 列表
    /// </summary>
    private List<ToolDefinition> BuildToolDefinitions()
    {
        var definitions = new List<ToolDefinition>();

        // 1. Registry tools (core + user-registered)
        foreach (var tool in _toolRegistry.GetAllTools())
        {
            definitions.Add(
                new ToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersSchema = tool.ParametersSchema,
                }
            );
        }

        // 2. create_tool (if session available)
        if (_createToolTool != null)
        {
            definitions.Add(
                new ToolDefinition
                {
                    Name = _createToolTool.Name,
                    Description = _createToolTool.Description,
                    ParametersSchema = _createToolTool.ParametersSchema,
                }
            );
        }

        // 3. Session tools (dynamically created ephemeral tools)
        if (_toolSession != null)
        {
            foreach (var tool in _toolSession.GetSessionTools())
            {
                definitions.Add(
                    new ToolDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        ParametersSchema = tool.ParametersSchema,
                    }
                );
            }
        }

        return definitions;
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
    protected override string? ExtractFinalAnswer(AgentStep step, int maxSteps)
    {
        throw new NotSupportedException(
            "FunctionCallingAgent 使用 Native Function Calling 循环，不走 ExtractFinalAnswer 路径"
        );
    }
}
