namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Structured logger for agent operations.
/// </summary>
public sealed class AgentLogger
{
    private readonly ILogger _logger;
    private readonly string _agentName;
    private readonly TelemetryConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLogger"/> class.
    /// </summary>
    public AgentLogger(ILogger logger, string agentName, TelemetryConfig config)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentNullException.ThrowIfNull(config);
        _logger = logger;
        _agentName = agentName;
        _config = config;
    }

    /// <summary>
    /// Logs the start of a request.
    /// </summary>
    public void LogRequestStart(string requestId, string input)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogInformation(
            "Agent {AgentName} started request {RequestId}. Input length: {InputLength}",
            _agentName,
            requestId,
            input.Length
        );
    }

    /// <summary>
    /// Logs the completion of a request.
    /// </summary>
    public void LogRequestComplete(
        string requestId,
        bool success,
        TimeSpan duration,
        int? tokensUsed = null
    )
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        if (success)
        {
            _logger.LogInformation(
                "Agent {AgentName} completed request {RequestId} in {DurationMs}ms. Tokens used: {TokensUsed}",
                _agentName,
                requestId,
                duration.TotalMilliseconds,
                tokensUsed ?? 0
            );
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentName} request {RequestId} failed in {DurationMs}ms",
                _agentName,
                requestId,
                duration.TotalMilliseconds
            );
        }
    }

    /// <summary>
    /// Logs a tool invocation.
    /// </summary>
    public void LogToolCall(string requestId, string toolName, bool success, TimeSpan duration)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogDebug(
            "Agent {AgentName} invoked tool {ToolName} for request {RequestId}. Success: {Success}, Duration: {DurationMs}ms",
            _agentName,
            requestId,
            toolName,
            success,
            duration.TotalMilliseconds
        );
    }

    /// <summary>
    /// Logs an iteration.
    /// </summary>
    public void LogIteration(string requestId, int iteration, string thought)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogDebug(
            "Agent {AgentName} request {RequestId} iteration {Iteration}. Thought: {Thought}",
            _agentName,
            requestId,
            iteration,
            thought.Length > 100 ? thought[..100] + "..." : thought
        );
    }

    /// <summary>
    /// Logs an error.
    /// </summary>
    public void LogError(string requestId, Exception ex, string context)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogError(
            ex,
            "Agent {AgentName} request {RequestId} error. Context: {Context}",
            _agentName,
            requestId,
            context
        );
    }

    /// <summary>
    /// Logs a guardrail trigger.
    /// </summary>
    public void LogGuardrailTriggered(
        string requestId,
        string guardrailName,
        string action,
        string? reason = null
    )
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogWarning(
            "Agent {AgentName} guardrail {GuardrailName} triggered for request {RequestId}. Action: {Action}, Reason: {Reason}",
            _agentName,
            guardrailName,
            requestId,
            action,
            reason ?? "N/A"
        );
    }

    /// <summary>
    /// Logs an LLM call.
    /// </summary>
    public void LogLLMCall(
        string requestId,
        string model,
        int promptTokens,
        int completionTokens,
        TimeSpan duration
    )
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogDebug(
            "Agent {AgentName} request {RequestId} LLM call. Model: {Model}, Prompt tokens: {PromptTokens}, Completion tokens: {CompletionTokens}, Duration: {DurationMs}ms",
            _agentName,
            requestId,
            model,
            promptTokens,
            completionTokens,
            duration.TotalMilliseconds
        );
    }
}
