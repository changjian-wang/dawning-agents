using System.Text;
using System.Text.RegularExpressions;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// ReAct pattern-based agent implementation.
/// </summary>
/// <remarks>
/// <para>ReAct = Reasoning + Acting, alternating between thinking and acting.</para>
/// <para>Output format: Thought → Action → Action Input → Observation → ... → Final Answer.</para>
/// <para>Reference: ReAct: Synergizing Reasoning and Acting in Language Models (Yao et al., 2022).</para>
/// </remarks>
public partial class ReActAgent : AgentBase
{
    private readonly IToolReader? _toolRegistry;
    private readonly ISkillRouter? _skillRouter;
    private readonly IReflectionEngine? _reflectionEngine;

    /// <summary>
    /// Matches the "Thought: ..." section and extracts the agent's reasoning process.
    /// </summary>
    [GeneratedRegex(@"Thought:\s*(.+?)(?=Action:|Final Answer:|$)", RegexOptions.Singleline)]
    private static partial Regex ThoughtRegex();

    /// <summary>
    /// Matches the "Action: ..." section and extracts the action name to execute.
    /// </summary>
    [GeneratedRegex(@"Action:\s*(.+?)(?=Action Input:|$)", RegexOptions.Singleline)]
    private static partial Regex ActionRegex();

    /// <summary>
    /// Matches the "Action Input: ..." section and extracts the action's input parameters.
    /// </summary>
    [GeneratedRegex(@"Action Input:\s*(.+?)(?=Observation:|$)", RegexOptions.Singleline)]
    private static partial Regex ActionInputRegex();

    /// <summary>
    /// Matches the "Final Answer: ..." section and extracts the final answer.
    /// </summary>
    [GeneratedRegex(@"Final Answer:\s*(.+?)$", RegexOptions.Singleline)]
    private static partial Regex FinalAnswerRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReActAgent"/> class.
    /// </summary>
    /// <param name="llmProvider">LLM provider for calling language models.</param>
    /// <param name="options">Agent configuration options.</param>
    /// <param name="toolRegistry">Tool read-only query (optional).</param>
    /// <param name="memory">Conversation memory (optional).</param>
    /// <param name="logger">Logger (optional).</param>
    /// <param name="usageTracker">Tool usage tracker (optional).</param>
    /// <param name="skillRouter">Semantic skill router (optional).</param>
    /// <param name="reflectionEngine">Reflection engine (optional).</param>
    public ReActAgent(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IToolReader? toolRegistry = null,
        IConversationMemory? memory = null,
        ILogger<ReActAgent>? logger = null,
        IToolUsageTracker? usageTracker = null,
        ISkillRouter? skillRouter = null,
        IReflectionEngine? reflectionEngine = null
    )
        : base(llmProvider, options, memory, logger, usageTracker)
    {
        _toolRegistry = toolRegistry;
        _skillRouter = skillRouter;
        _reflectionEngine = reflectionEngine;
    }

    /// <summary>
    /// Executes a single ReAct step: build prompt → call LLM → parse output → execute action.
    /// </summary>
    /// <param name="context">Current execution context containing user input and historical steps.</param>
    /// <param name="stepNumber">Current step number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Step result containing Thought, Action, ActionInput, and Observation.</returns>
    protected override async Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    )
    {
        // Build prompt
        var prompt = BuildPrompt(context);

        // Build system prompt (use async semantic routing when SkillRouter is available)
        string systemPrompt;
        if (_skillRouter != null)
        {
            var availableActions = await BuildAvailableActionsPromptAsync(
                    context.UserInput,
                    cancellationToken
                )
                .ConfigureAwait(false);
            systemPrompt = FormatSystemPrompt(availableActions);
        }
        else
        {
            systemPrompt = BuildSystemPrompt();
        }

        // Call LLM
        var messages = new List<ChatMessage>(2)
        {
            new("system", systemPrompt),
            new("user", prompt),
        };

        var response = await LLMProvider
            .ChatAsync(
                messages,
                new ChatCompletionOptions { MaxTokens = Options.MaxTokens },
                cancellationToken
            )
            .ConfigureAwait(false);
        var llmOutput = response.Content ?? string.Empty;

        Logger.LogDebug("LLM output:\n{Output}", llmOutput);

        // Parse output
        var thought = ExtractMatch(ThoughtRegex(), llmOutput);
        var action = ExtractMatch(ActionRegex(), llmOutput);
        var actionInput = ExtractMatch(ActionInputRegex(), llmOutput);

        // Execute action and get observation
        string? observation = null;
        if (!string.IsNullOrEmpty(action))
        {
            observation = await ExecuteActionAsync(action, actionInput, cancellationToken)
                .ConfigureAwait(false);
        }

        return new AgentStep
        {
            StepNumber = stepNumber,
            RawOutput = llmOutput,
            Thought = thought,
            Action = action,
            ActionInput = actionInput,
            Observation = observation,
            Cost = EstimateStepCost(response),
        };
    }

    /// <summary>
    /// Extracts the final answer from a step.
    /// </summary>
    /// <param name="step">Current execution step.</param>
    /// <returns>The final answer string, or <see langword="null"/> if the step does not contain a final answer.</returns>
    /// <remarks>
    /// Returns the answer content when the LLM output contains "Final Answer: ...".
    /// </remarks>
    protected override string? ExtractFinalAnswer(AgentStep step, int maxSteps)
    {
        // Extract Final Answer from raw output
        if (!string.IsNullOrEmpty(step.RawOutput))
        {
            var finalAnswer = ExtractMatch(FinalAnswerRegex(), step.RawOutput);
            if (!string.IsNullOrEmpty(finalAnswer))
            {
                return finalAnswer;
            }
        }

        // If no Action but has Thought, return as fallback only when max steps reached
        if (
            string.IsNullOrEmpty(step.Action)
            && !string.IsNullOrEmpty(step.Thought)
            && step.StepNumber >= maxSteps
        )
        {
            // Return Thought as answer
            return step.Thought;
        }

        // If no standard format, no Thought, but has raw output, return raw output directly
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
    /// Builds the system prompt.
    /// </summary>
    protected virtual string BuildSystemPrompt()
    {
        var availableActions = BuildAvailableActionsPrompt();
        return FormatSystemPrompt(availableActions);
    }

    /// <summary>
    /// Formats the available actions list into a complete system prompt.
    /// </summary>
    private string FormatSystemPrompt(string availableActions)
    {
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
    /// Builds the available actions prompt.
    /// </summary>
    protected virtual string BuildAvailableActionsPrompt()
    {
        if (_toolRegistry == null || _toolRegistry.Count == 0)
        {
            return "No tools available. Answer the question directly using your knowledge.";
        }

        var tools = _toolRegistry.GetAllTools();

        var sb = new StringBuilder();
        sb.AppendLine("Available actions:");

        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the available actions prompt with semantic routing support.
    /// </summary>
    protected virtual async Task<string> BuildAvailableActionsPromptAsync(
        string taskDescription,
        CancellationToken cancellationToken = default
    )
    {
        if (_toolRegistry == null || _toolRegistry.Count == 0)
        {
            return "No tools available. Answer the question directly using your knowledge.";
        }

        IEnumerable<ITool> tools;

        // If SkillRouter is registered and tool count meets threshold, use semantic routing for top-K retrieval
        if (_skillRouter != null)
        {
            var scored = await _skillRouter
                .RouteAsync(taskDescription, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            tools = scored.Select(s => s.Tool);
            Logger.LogDebug("SkillRouter selected {Count} tools for task", scored.Count);
        }
        else
        {
            tools = _toolRegistry.GetAllTools();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Available actions:");

        foreach (var tool in tools)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the user prompt including historical steps.
    /// </summary>
    /// <param name="context">Execution context containing user input and historical steps.</param>
    /// <returns>The formatted prompt string.</returns>
    /// <remarks>
    /// Output format example:
    /// <code>
    /// Question: User question
    ///
    /// Thought: Previous step's reasoning
    /// Action: Previous step's action
    /// Action Input: Previous step's input
    /// Observation: Previous step's observation
    /// </code>
    /// </remarks>
    protected virtual string BuildPrompt(AgentContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Question: {context.UserInput}");
        sb.AppendLine();

        // Append historical steps
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
    /// Executes an action and returns the observation.
    /// </summary>
    /// <param name="action">Action name (e.g., Search, Calculate, Lookup).</param>
    /// <param name="actionInput">Action input parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The observation after action execution.</returns>
    /// <remarks>
    /// <para>Uses tools registered in IToolRegistry to execute actions.</para>
    /// <para>If the tool does not exist, returns an error message with available tools.</para>
    /// </remarks>
    protected virtual async Task<string> ExecuteActionAsync(
        string action,
        string? actionInput,
        CancellationToken cancellationToken
    )
    {
        Logger.LogDebug("Executing action: {Action}, input: {Input}", action, actionInput);

        // Use registered tools
        if (_toolRegistry != null)
        {
            var tool = _toolRegistry.GetTool(action);
            if (tool != null)
            {
                Logger.LogDebug("Using registered tool: {ToolName}", tool.Name);
                var result = await tool.ExecuteAsync(actionInput ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    return result.Output;
                }

                // Tool execution failed — attempt reflection repair
                if (_reflectionEngine != null)
                {
                    var repaired = await TryReflectAndRepairAsync(
                            tool,
                            actionInput ?? string.Empty,
                            result,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    if (repaired != null)
                    {
                        return repaired;
                    }
                }

                return $"Tool error: {result.Error}";
            }

            // Tool not found, return error with available tools list
            var availableTools = _toolRegistry.GetAllTools();
            if (availableTools.Count > 0)
            {
                var toolList = string.Join(", ", availableTools.Select(t => t.Name));
                Logger.LogWarning("Tool '{Action}' not found, available tools: {Tools}", action, toolList);
                return $"Error: Tool '{action}' not found. Available tools: {toolList}. Please choose a valid tool.";
            }
        }

        // No tools registered
        Logger.LogWarning("No tools registered, cannot execute action: {Action}", action);
        return $"Error: No tools registered. Cannot execute action '{action}'. Please register tools using AddBuiltInTools() or AddToolsFrom<T>().";
    }

    /// <summary>
    /// Attempts to repair a failed tool execution through the reflection engine.
    /// </summary>
    private async Task<string?> TryReflectAndRepairAsync(
        ITool failedTool,
        string input,
        ToolResult failedResult,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var context = new ReflectionContext
            {
                FailedTool = failedTool,
                Input = input,
                FailedResult = failedResult,
                TaskDescription = "Tool execution failed",
                UsageStats =
                    UsageTracker != null
                        ? await UsageTracker
                            .GetStatsAsync(failedTool.Name, cancellationToken)
                            .ConfigureAwait(false)
                        : null,
            };

            var reflection = await _reflectionEngine!
                .ReflectAsync(context, cancellationToken)
                .ConfigureAwait(false);

            Logger.LogInformation(
                "Reflection on '{ToolName}': Action={Action}, Confidence={Confidence:F2}, Diagnosis={Diagnosis}",
                failedTool.Name,
                reflection.Action,
                reflection.Confidence,
                reflection.Diagnosis
            );

            if (reflection.Action == ReflectionAction.Retry)
            {
                // Simple retry
                var retryResult = await failedTool
                    .ExecuteAsync(input, cancellationToken)
                    .ConfigureAwait(false);

                return retryResult.Success ? retryResult.Output : null;
            }

            // ReviseAndRetry、Abandon、CreateNew、Escalate — 返回诊断信息给 LLM 决策
            if (reflection.Diagnosis != null)
            {
                return $"Tool error: {failedResult.Error} (Reflection: {reflection.Diagnosis})";
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Reflection failed for tool '{ToolName}'", failedTool.Name);
        }

        return null;
    }

    /// <summary>
    /// Extracts content from a regex match.
    /// </summary>
    private static string? ExtractMatch(Regex regex, string input)
    {
        var match = regex.Match(input);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
