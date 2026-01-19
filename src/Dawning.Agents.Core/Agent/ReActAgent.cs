using System.Text;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// 基于 ReAct 模式的 Agent 实现
/// </summary>
/// <remarks>
/// <para>ReAct = Reasoning + Acting，交替进行思考和行动</para>
/// <para>输出格式：Thought → Action → Action Input → Observation → ... → Final Answer</para>
/// <para>参考论文：ReAct: Synergizing Reasoning and Acting in Language Models (Yao et al., 2022)</para>
/// </remarks>
public partial class ReActAgent : AgentBase
{
    private readonly IToolRegistry? _toolRegistry;

    /// <summary>
    /// 匹配 "Thought: ..." 部分，提取 Agent 的思考过程
    /// </summary>
    [GeneratedRegex(@"Thought:\s*(.+?)(?=Action:|Final Answer:|$)", RegexOptions.Singleline)]
    private static partial Regex ThoughtRegex();

    /// <summary>
    /// 匹配 "Action: ..." 部分，提取要执行的动作名称
    /// </summary>
    [GeneratedRegex(@"Action:\s*(.+?)(?=Action Input:|$)", RegexOptions.Singleline)]
    private static partial Regex ActionRegex();

    /// <summary>
    /// 匹配 "Action Input: ..." 部分，提取动作的输入参数
    /// </summary>
    [GeneratedRegex(@"Action Input:\s*(.+?)(?=Observation:|$)", RegexOptions.Singleline)]
    private static partial Regex ActionInputRegex();

    /// <summary>
    /// 匹配 "Final Answer: ..." 部分，提取最终答案
    /// </summary>
    [GeneratedRegex(@"Final Answer:\s*(.+?)$", RegexOptions.Singleline)]
    private static partial Regex FinalAnswerRegex();

    /// <summary>
    /// 初始化 ReAct Agent
    /// </summary>
    /// <param name="llmProvider">LLM 提供者，用于调用语言模型</param>
    /// <param name="options">Agent 配置选项</param>
    /// <param name="toolRegistry">工具注册表（可选）</param>
    /// <param name="logger">日志记录器（可选）</param>
    public ReActAgent(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IToolRegistry? toolRegistry = null,
        ILogger<ReActAgent>? logger = null
    )
        : base(llmProvider, options, logger)
    {
        _toolRegistry = toolRegistry;
    }

    /// <summary>
    /// 执行单个 ReAct 步骤：构建提示词 → 调用 LLM → 解析输出 → 执行动作
    /// </summary>
    /// <param name="context">当前执行上下文，包含用户输入和历史步骤</param>
    /// <param name="stepNumber">当前步骤编号（从 1 开始）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果，包含 Thought、Action、ActionInput 和 Observation</returns>
    protected override async Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    )
    {
        // 构建提示词
        var prompt = BuildPrompt(context);

        // 调用 LLM
        var messages = new List<ChatMessage>
        {
            new("system", BuildSystemPrompt()),
            new("user", prompt),
        };

        var response = await LLMProvider.ChatAsync(
            messages,
            new ChatCompletionOptions { MaxTokens = 1024 },
            cancellationToken
        );
        var llmOutput = response.Content ?? string.Empty;

        Logger.LogDebug("LLM 输出:\n{Output}", llmOutput);

        // 解析输出
        var thought = ExtractMatch(ThoughtRegex(), llmOutput);
        var action = ExtractMatch(ActionRegex(), llmOutput);
        var actionInput = ExtractMatch(ActionInputRegex(), llmOutput);

        // 执行动作获取观察结果
        string? observation = null;
        if (!string.IsNullOrEmpty(action))
        {
            observation = await ExecuteActionAsync(action, actionInput, cancellationToken);
        }

        return new AgentStep
        {
            StepNumber = stepNumber,
            RawOutput = llmOutput,
            Thought = thought,
            Action = action,
            ActionInput = actionInput,
            Observation = observation,
        };
    }

    /// <summary>
    /// 从步骤中提取最终答案
    /// </summary>
    /// <param name="step">当前执行步骤</param>
    /// <returns>最终答案字符串，如果该步骤不包含最终答案则返回 null</returns>
    /// <remarks>
    /// 当 LLM 输出包含 "Final Answer: ..." 时返回答案内容
    /// </remarks>
    protected override string? ExtractFinalAnswer(AgentStep step)
    {
        // 从原始输出中提取 Final Answer
        if (!string.IsNullOrEmpty(step.RawOutput))
        {
            var finalAnswer = ExtractMatch(FinalAnswerRegex(), step.RawOutput);
            if (!string.IsNullOrEmpty(finalAnswer))
            {
                return finalAnswer;
            }
        }

        // 如果没有 Action 且有 Thought，可能 LLM 直接给出了答案（非标准格式）
        if (string.IsNullOrEmpty(step.Action) && !string.IsNullOrEmpty(step.Thought))
        {
            // 将 Thought 作为答案返回
            return step.Thought;
        }

        // 如果既没有标准格式，也没有 Thought，但有原始输出，直接返回原始输出
        if (
            string.IsNullOrEmpty(step.Action)
            && string.IsNullOrEmpty(step.Thought)
            && !string.IsNullOrEmpty(step.RawOutput)
        )
        {
            return step.RawOutput;
        }

        return null;
    }

    /// <summary>
    /// 构建系统提示词
    /// </summary>
    protected virtual string BuildSystemPrompt()
    {
        var availableActions = BuildAvailableActionsPrompt();

        return $"""
            {Instructions}

            You are an AI assistant that follows the ReAct pattern.
            When answering questions, use the following format:

            Thought: [Your reasoning about what to do]
            Action: [The action to take]
            Action Input: [The input for the action]

            After receiving the observation from the action, continue with:
            Thought: [Your updated reasoning]
            ...

            When you have enough information to provide the final answer, use:
            Final Answer: [Your complete answer to the user's question]

            {availableActions}

            Always think step by step and explain your reasoning.
            """;
    }

    /// <summary>
    /// 构建可用动作列表的提示词
    /// </summary>
    protected virtual string BuildAvailableActionsPrompt()
    {
        if (_toolRegistry == null || _toolRegistry.Count == 0)
        {
            // 没有注册工具时使用默认列表
            return """
                Available actions:
                - Search: Search for information
                - Calculate: Perform calculations
                - Lookup: Look up specific data
                """;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Available actions:");

        foreach (var tool in _toolRegistry.GetAllTools())
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建用户提示词（包含历史步骤）
    /// </summary>
    /// <param name="context">执行上下文，包含用户输入和历史步骤</param>
    /// <returns>格式化的提示词字符串</returns>
    /// <remarks>
    /// 输出格式示例：
    /// <code>
    /// Question: 用户问题
    ///
    /// Thought: 上一步的思考
    /// Action: 上一步的动作
    /// Action Input: 上一步的输入
    /// Observation: 上一步的观察结果
    /// </code>
    /// </remarks>
    protected virtual string BuildPrompt(AgentContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Question: {context.UserInput}");
        sb.AppendLine();

        // 添加历史步骤
        foreach (var step in context.Steps)
        {
            if (!string.IsNullOrEmpty(step.Thought))
            {
                sb.AppendLine($"Thought: {step.Thought}");
            }

            if (!string.IsNullOrEmpty(step.Action))
            {
                sb.AppendLine($"Action: {step.Action}");
            }

            if (!string.IsNullOrEmpty(step.ActionInput))
            {
                sb.AppendLine($"Action Input: {step.ActionInput}");
            }

            if (!string.IsNullOrEmpty(step.Observation))
            {
                sb.AppendLine($"Observation: {step.Observation}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 执行动作并返回观察结果
    /// </summary>
    /// <param name="action">动作名称（如 Search, Calculate, Lookup）</param>
    /// <param name="actionInput">动作输入参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>动作执行后的观察结果</returns>
    /// <remarks>
    /// <para>优先使用 IToolRegistry 中注册的工具</para>
    /// <para>如果没有找到注册的工具，回退到内置的 Mock 实现</para>
    /// </remarks>
    protected virtual async Task<string> ExecuteActionAsync(
        string action,
        string? actionInput,
        CancellationToken cancellationToken
    )
    {
        Logger.LogDebug("执行动作: {Action}, 输入: {Input}", action, actionInput);

        // 优先使用注册的工具
        if (_toolRegistry != null)
        {
            var tool = _toolRegistry.GetTool(action);
            if (tool != null)
            {
                Logger.LogDebug("使用注册的工具: {ToolName}", tool.Name);
                var result = await tool.ExecuteAsync(
                    actionInput ?? string.Empty,
                    cancellationToken
                );

                if (result.Success)
                {
                    return result.Output;
                }
                else
                {
                    return $"Tool error: {result.Error}";
                }
            }
        }

        // 回退到 Mock 实现
        Logger.LogDebug("使用内置 Mock 工具: {Action}", action);
        var mockResult = action.ToLowerInvariant() switch
        {
            "search" =>
                $"Search results for '{actionInput}': [This is a mock result. Register real tools for actual functionality.]",
            "calculate" => $"Calculation result: [Mock calculation for '{actionInput}']",
            "lookup" => $"Lookup result for '{actionInput}': [Mock data]",
            _ => $"Unknown action '{action}'. Use --help to see available actions.",
        };

        return mockResult;
    }

    /// <summary>
    /// 从正则匹配中提取内容
    /// </summary>
    private static string? ExtractMatch(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
