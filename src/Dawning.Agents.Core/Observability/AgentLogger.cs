namespace Dawning.Agents.Core.Observability;

using Dawning.Agents.Abstractions.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Agent 结构化日志器
/// </summary>
public class AgentLogger
{
    private readonly ILogger _logger;
    private readonly string _agentName;
    private readonly TelemetryConfig _config;

    /// <summary>
    /// 创建 Agent 日志器
    /// </summary>
    public AgentLogger(ILogger logger, string agentName, TelemetryConfig config)
    {
        _logger = logger;
        _agentName = agentName;
        _config = config;
    }

    /// <summary>
    /// 记录请求开始
    /// </summary>
    public void LogRequestStart(string requestId, string input)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogInformation(
            "Agent {AgentName} 开始请求 {RequestId}。输入长度：{InputLength}",
            _agentName,
            requestId,
            input.Length
        );
    }

    /// <summary>
    /// 记录请求完成
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
                "Agent {AgentName} 完成请求 {RequestId}，耗时 {DurationMs}ms。Token数：{TokensUsed}",
                _agentName,
                requestId,
                duration.TotalMilliseconds,
                tokensUsed ?? 0
            );
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentName} 请求 {RequestId} 失败，耗时 {DurationMs}ms",
                _agentName,
                requestId,
                duration.TotalMilliseconds
            );
        }
    }

    /// <summary>
    /// 记录工具调用
    /// </summary>
    public void LogToolCall(string requestId, string toolName, bool success, TimeSpan duration)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogDebug(
            "Agent {AgentName} 为请求 {RequestId} 调用工具 {ToolName}。成功：{Success}，耗时：{DurationMs}ms",
            _agentName,
            requestId,
            toolName,
            success,
            duration.TotalMilliseconds
        );
    }

    /// <summary>
    /// 记录迭代
    /// </summary>
    public void LogIteration(string requestId, int iteration, string thought)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogDebug(
            "Agent {AgentName} 请求 {RequestId} 第 {Iteration} 次迭代。思考：{Thought}",
            _agentName,
            requestId,
            iteration,
            thought.Length > 100 ? thought[..100] + "..." : thought
        );
    }

    /// <summary>
    /// 记录错误
    /// </summary>
    public void LogError(string requestId, Exception ex, string context)
    {
        if (!_config.EnableLogging)
        {
            return;
        }

        _logger.LogError(
            ex,
            "Agent {AgentName} 请求 {RequestId} 错误。上下文：{Context}",
            _agentName,
            requestId,
            context
        );
    }

    /// <summary>
    /// 记录护栏触发
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
            "Agent {AgentName} 护栏 {GuardrailName} 在请求 {RequestId} 触发。操作：{Action}，原因：{Reason}",
            _agentName,
            guardrailName,
            requestId,
            action,
            reason ?? "无"
        );
    }

    /// <summary>
    /// 记录 LLM 调用
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
            "Agent {AgentName} 请求 {RequestId} LLM调用。模型：{Model}，提示词：{PromptTokens}，补全：{CompletionTokens}，耗时：{DurationMs}ms",
            _agentName,
            requestId,
            model,
            promptTokens,
            completionTokens,
            duration.TotalMilliseconds
        );
    }
}
