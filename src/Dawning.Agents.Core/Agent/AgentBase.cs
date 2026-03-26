using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.Agent;

/// <summary>
/// Agent base class that provides a common execution framework.
/// </summary>
/// <remarks>
/// <para>Implements the core agent loop: Observe → Think → Act.</para>
/// <para>Subclasses only need to implement <see cref="ExecuteStepAsync"/> and <see cref="ExtractFinalAnswer"/>.</para>
/// </remarks>
public abstract class AgentBase : IAgent
{
    /// <summary>
    /// LLM provider for interacting with language models.
    /// </summary>
    protected readonly ILLMProvider LLMProvider;

    /// <summary>
    /// Logger instance.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Agent configuration options.
    /// </summary>
    protected readonly AgentOptions Options;

    /// <summary>
    /// Conversation memory (optional) for maintaining context across sessions.
    /// </summary>
    protected readonly IConversationMemory? Memory;

    /// <summary>
    /// Tool usage tracker (optional) for recording tool execution statistics.
    /// </summary>
    protected readonly IToolUsageTracker? UsageTracker;

    /// <summary>
    /// Gets the agent name.
    /// </summary>
    public virtual string Name => Options.Name;

    /// <summary>
    /// Gets the agent system instructions.
    /// </summary>
    public virtual string Instructions => Options.Instructions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentBase"/> class.
    /// </summary>
    /// <param name="llmProvider">LLM provider.</param>
    /// <param name="options">Agent configuration options.</param>
    /// <param name="memory">Conversation memory (optional).</param>
    /// <param name="logger">Logger (optional).</param>
    /// <param name="usageTracker">Tool usage tracker (optional).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="llmProvider"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    protected AgentBase(
        ILLMProvider llmProvider,
        IOptions<AgentOptions> options,
        IConversationMemory? memory = null,
        ILogger? logger = null,
        IToolUsageTracker? usageTracker = null
    )
    {
        LLMProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        Memory = memory;
        Logger = logger ?? NullLogger.Instance;
        UsageTracker = usageTracker;
    }

    /// <summary>
    /// Executes the agent task.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent response.</returns>
    public virtual Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input, MaxSteps = Options.MaxSteps };
        return RunAsync(context, cancellationToken);
    }

    /// <summary>
    /// Executes the agent task with the specified context.
    /// </summary>
    /// <param name="context">Execution context containing user input and historical steps.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent response.</returns>
    public virtual async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var costTracker = CreateCostTracker();
        Logger.LogInformation(
            "Agent {AgentName} started task execution, input length: {InputLength}",
            Name,
            context.UserInput.Length
        );

        try
        {
            while (context.Steps.Count < context.MaxSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepNumber = context.Steps.Count + 1;
                Logger.LogDebug("Executing step {StepNumber}/{MaxSteps}", stepNumber, context.MaxSteps);

                // Execute single step
                var step = await ExecuteStepAsync(context, stepNumber, cancellationToken)
                    .ConfigureAwait(false);
                context.AddStep(step);

                // Record tool usage
                await RecordToolUsageAsync(step, cancellationToken).ConfigureAwait(false);

                // Accumulate cost and check budget
                costTracker?.Add(step.Cost);

                // Check for final answer
                var finalAnswer = ExtractFinalAnswer(step, context.MaxSteps);
                if (finalAnswer != null)
                {
                    stopwatch.Stop();
                    Logger.LogInformation(
                        "Agent {AgentName} completed task in {StepCount} steps",
                        Name,
                        context.Steps.Count
                    );

                    // Save conversation to memory
                    await SaveToMemoryAsync(context.UserInput, finalAnswer, cancellationToken)
                        .ConfigureAwait(false);

                    return AgentResponse.Successful(finalAnswer, context.Steps, stopwatch.Elapsed);
                }
            }

            // Exceeded maximum steps
            stopwatch.Stop();
            Logger.LogWarning("Agent {AgentName} exceeded maximum steps {MaxSteps}", Name, context.MaxSteps);
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
                "Agent {AgentName} cost exceeded budget: {TotalCost:F4} > {Budget:F4}",
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
            Logger.LogWarning(ex, "Agent {AgentName} task was cancelled", Name);
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
            Logger.LogError(ex, "Agent {AgentName} execution error", Name);
            return AgentResponse.Failed(ex.Message, context.Steps, stopwatch.Elapsed, ex);
        }
    }

    /// <summary>
    /// Executes a single step. Subclasses implement specific reasoning and action logic.
    /// </summary>
    /// <param name="context">Current execution context containing user input and historical steps.</param>
    /// <param name="stepNumber">Current step number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Step result containing thought, action, and observation.</returns>
    protected abstract Task<AgentStep> ExecuteStepAsync(
        AgentContext context,
        int stepNumber,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Extracts the final answer from a step.
    /// </summary>
    /// <param name="step">Current execution step.</param>
    /// <param name="maxSteps">Maximum steps for the current context.</param>
    /// <returns>The final answer string, or <see langword="null"/> if the step does not contain a final answer.</returns>
    /// <remarks>
    /// When a non-null value is returned, the agent loop terminates and returns a successful response.
    /// </remarks>
    protected abstract string? ExtractFinalAnswer(AgentStep step, int maxSteps);

    /// <summary>
    /// Estimates step cost (USD) from an LLM response.
    /// </summary>
    /// <param name="response">LLM response.</param>
    /// <returns>Estimated cost.</returns>
    protected static decimal EstimateStepCost(ChatCompletionResponse response)
    {
        return ModelPricing
            .KnownPricing.GetPricing("default")
            .CalculateCost(response.PromptTokens, response.CompletionTokens);
    }

    /// <summary>
    /// Creates a cost tracker if a budget is configured.
    /// </summary>
    protected CostTracker? CreateCostTracker()
    {
        return Options.MaxCostPerRun.HasValue ? new CostTracker(Options.MaxCostPerRun.Value) : null;
    }

    /// <summary>
    /// Records tool usage to the tracker if configured and the step contains an action.
    /// </summary>
    private async Task RecordToolUsageAsync(AgentStep step, CancellationToken cancellationToken)
    {
        if (UsageTracker == null || string.IsNullOrEmpty(step.Action))
        {
            return;
        }

        try
        {
            var isSuccess =
                step.Observation != null
                && !step.Observation.StartsWith("Tool error:", StringComparison.OrdinalIgnoreCase);
            var record = new ToolUsageRecord
            {
                ToolName = step.Action,
                Success = isSuccess,
                Duration = TimeSpan.Zero,
                ErrorMessage = isSuccess ? null : step.Observation,
                TaskContext = step.Thought,
            };

            await UsageTracker.RecordUsageAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to record tool usage for '{ToolName}'", step.Action);
        }
    }

    /// <summary>
    /// Saves the conversation to memory if configured.
    /// </summary>
    /// <param name="userInput">User input.</param>
    /// <param name="assistantResponse">Agent response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SaveToMemoryAsync(
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
            await Memory
                .AddMessageAsync(
                    new ConversationMessage { Role = "user", Content = userInput },
                    cancellationToken
                )
                .ConfigureAwait(false);
            await Memory
                .AddMessageAsync(
                    new ConversationMessage { Role = "assistant", Content = assistantResponse },
                    cancellationToken
                )
                .ConfigureAwait(false);
            Logger.LogDebug("Conversation saved to memory, current message count: {Count}", Memory.MessageCount);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error saving conversation to memory");
        }
    }
}
