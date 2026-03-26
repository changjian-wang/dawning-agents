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
/// Native Function Calling-based agent implementation.
/// </summary>
/// <remarks>
/// <para>Uses LLM native Function Calling (ToolCalls) instead of text parsing.</para>
/// <para>Flow: build messages → ChatAsync → detect ToolCalls → execute tools → return results → loop.</para>
/// <para>Compared to ReActAgent (regex-based parsing), this approach is more reliable and accurate.</para>
/// <para>When IToolSession is injected, supports dynamic tool creation (create_tool) and session/user/global tool loading.</para>
/// </remarks>
public class FunctionCallingAgent : AgentBase
{
    private readonly IToolReader _toolRegistry;
    private readonly IToolSession? _toolSession;
    private readonly CreateToolTool? _createToolTool;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionCallingAgent"/> class.
    /// </summary>
    /// <param name="llmProvider">LLM provider.</param>
    /// <param name="options">Agent configuration options.</param>
    /// <param name="toolRegistry">Tool read-only query (required).</param>
    /// <param name="memory">Conversation memory (optional).</param>
    /// <param name="toolSession">Tool session (optional, enables dynamic tool creation).</param>
    /// <param name="logger">Logger (optional).</param>
    /// <param name="usageTracker">Tool usage tracker (optional).</param>
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
    /// Executes the agent task using Function Calling mode.
    /// </summary>
    /// <remarks>
    /// Overrides the base RunAsync to implement the full Function Calling loop:
    /// <list type="number">
    /// <item>Build message list (with tool definitions).</item>
    /// <item>Call LLM.</item>
    /// <item>If response contains ToolCalls: execute tools → return results → continue loop.</item>
    /// <item>If response does not contain ToolCalls: return final answer.</item>
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
            "FunctionCallingAgent {AgentName} started task execution, input length: {InputLength}",
            Name,
            context.UserInput.Length
        );

        try
        {
            // Build message history
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(Instructions))
            {
                messages.Add(ChatMessage.System(Instructions));
            }

            // Load history from Memory
            if (Memory != null)
            {
                var history = await Memory
                    .GetContextAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                messages.AddRange(history);
            }

            messages.Add(ChatMessage.User(context.UserInput));

            // Function Calling loop
            var step = 0;
            // Message count limit: each step adds 1 assistant + N tool results, limit total messages to prevent OOM
            var maxMessages = context.MaxSteps * 10 + messages.Count;
            while (step < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (messages.Count > maxMessages)
                {
                    Logger.LogWarning(
                        "FunctionCallingAgent {AgentName} message count exceeded limit {Count}/{Max}",
                        Name,
                        messages.Count,
                        maxMessages
                    );

                    context.AddStep(
                        new AgentStep
                        {
                            StepNumber = step + 1,
                            Thought = "Message count exceeded limit, terminating loop",
                            Action = "Overflow",
                            Observation = $"Message count {messages.Count} exceeded limit {maxMessages}",
                        }
                    );
                    break;
                }

                step++;

                // Rebuild tool definitions each iteration — session tools may have changed
                // (e.g. create_tool was called in a previous step)
                var toolDefinitions = BuildToolDefinitions();

                Logger.LogDebug("Function Calling step {Step}/{MaxSteps}", step, context.MaxSteps);

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
                    // LLM requested tool calls
                    Logger.LogDebug("Received {Count} tool call requests", response.ToolCalls!.Count);

                    // Add assistant message (with tool calls)
                    messages.Add(
                        ChatMessage.AssistantWithToolCalls(response.ToolCalls!, response.Content)
                    );

                    // Execute each tool call and add result messages
                    var toolResultSummary = new StringBuilder();
                    foreach (var toolCall in response.ToolCalls!)
                    {
                        var toolResult = await ExecuteToolCallAsync(toolCall, cancellationToken)
                            .ConfigureAwait(false);

                        // Add tool result message
                        messages.Add(ChatMessage.ToolResult(toolCall.Id, toolResult));

                        toolResultSummary.AppendLine($"[{toolCall.FunctionName}]: {toolResult}");
                    }

                    // Record step
                    var toolNames = string.Join(
                        ", ",
                        response.ToolCalls!.Select(tc => tc.FunctionName)
                    );
                    context.AddStep(
                        new AgentStep
                        {
                            StepNumber = step,
                            RawOutput = response.Content,
                            Thought = $"Calling tools: {toolNames}",
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
                    // LLM returned final answer (no tool calls)
                    var finalAnswer = response.Content ?? string.Empty;

                    context.AddStep(
                        new AgentStep
                        {
                            StepNumber = step,
                            RawOutput = finalAnswer,
                            Thought = "Generating final answer",
                            Cost = stepCost,
                        }
                    );

                    stopwatch.Stop();
                    Logger.LogInformation(
                        "FunctionCallingAgent {AgentName} completed task in {Steps} steps",
                        Name,
                        context.Steps.Count
                    );

                    // Save to Memory
                    await SaveToMemoryAsync(context.UserInput, finalAnswer, cancellationToken)
                        .ConfigureAwait(false);

                    return AgentResponse.Successful(finalAnswer, context.Steps, stopwatch.Elapsed);
                }
            }

            // Exceeded maximum steps
            stopwatch.Stop();
            Logger.LogWarning(
                "FunctionCallingAgent {AgentName} exceeded maximum steps {MaxSteps}",
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
                "FunctionCallingAgent {AgentName} cost exceeded budget: {TotalCost:F4} > {Budget:F4}",
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
            Logger.LogWarning(ex, "FunctionCallingAgent {AgentName} task was cancelled", Name);
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
            Logger.LogError(ex, "FunctionCallingAgent {AgentName} execution error", Name);
            return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed, ex);
        }
    }

    /// <summary>
    /// Executes a single tool call.
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
            "Executing tool {ToolName}, arguments: {Args}",
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
            Logger.LogError(ex, "Tool {ToolName} execution failed", toolCall.FunctionName);
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
    /// Records tool call usage to the tracker.
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
    /// Resolves a tool: Registry → create_tool → Session tools.
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
    /// Builds a <see cref="ToolDefinition"/> list from IToolRegistry + IToolSession (deduplicated by name, Registry takes priority).
    /// </summary>
    private List<ToolDefinition> BuildToolDefinitions()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitions = new List<ToolDefinition>();

        // 1. Registry tools (core + user-registered) — highest priority
        foreach (var tool in _toolRegistry.GetAllTools())
        {
            if (seen.Add(tool.Name))
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

        // 2. create_tool (if session available)
        if (_createToolTool != null && seen.Add(_createToolTool.Name))
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

        // 3. Session tools (dynamically created ephemeral tools) — only add if not shadowed
        if (_toolSession != null)
        {
            foreach (var tool in _toolSession.GetSessionTools())
            {
                if (seen.Add(tool.Name))
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
        }

        return definitions;
    }

    // Minimal implementation of AgentBase abstract methods (FunctionCallingAgent overrides RunAsync and does not use these)

    /// <inheritdoc/>
    protected override Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException(
            "FunctionCallingAgent uses the native Function Calling loop and does not use the ExecuteStepAsync path."
        );
    }

    /// <inheritdoc/>
    protected override string? ExtractFinalAnswer(AgentStep step, int maxSteps)
    {
        throw new NotSupportedException(
            "FunctionCallingAgent uses the native Function Calling loop and does not use the ExtractFinalAnswer path."
        );
    }
}
