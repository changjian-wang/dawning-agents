using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 安全 Agent 包装器 - 为 Agent 添加护栏、速率限制和审计功能
/// </summary>
public class SafeAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly IGuardrailPipeline? _guardrailPipeline;
    private readonly IRateLimiter? _rateLimiter;
    private readonly IAuditLogger? _auditLogger;
    private readonly ILogger<SafeAgent> _logger;

    public SafeAgent(
        IAgent innerAgent,
        IGuardrailPipeline? guardrailPipeline = null,
        IRateLimiter? rateLimiter = null,
        IAuditLogger? auditLogger = null,
        ILogger<SafeAgent>? logger = null
    )
    {
        _innerAgent = innerAgent ?? throw new ArgumentNullException(nameof(innerAgent));
        _guardrailPipeline = guardrailPipeline;
        _rateLimiter = rateLimiter;
        _auditLogger = auditLogger;
        _logger = logger ?? NullLogger<SafeAgent>.Instance;
    }

    /// <inheritdoc />
    public string Name => _innerAgent.Name;

    /// <inheritdoc />
    public string Instructions => _innerAgent.Instructions;

    /// <inheritdoc />
    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(input, userId: null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 执行 Agent（带用户 ID）
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        string input,
        string? userId = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var rateLimitKey = userId ?? Guid.NewGuid().ToString("N");

        try
        {
            // 1. 审计日志 - 开始
            if (_auditLogger != null)
            {
                await _auditLogger.LogAsync(
                    new AuditEntry
                    {
                        EventType = AuditEventType.AgentRunStart,
                        AgentName = Name,
                        SessionId = rateLimitKey,
                        Input = input,
                    },
                    cancellationToken
                );
            }

            // 2. 速率限制检查
            if (_rateLimiter != null)
            {
                var rateLimitResult = await _rateLimiter.TryAcquireAsync(
                    rateLimitKey,
                    cancellationToken
                );

                if (!rateLimitResult.IsAllowed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} 被速率限制，需等待 {RetryAfter}",
                        Name,
                        rateLimitResult.RetryAfter
                    );

                    await LogAuditAsync(
                        AuditEventType.RateLimited,
                        rateLimitKey,
                        AuditResultStatus.RateLimited,
                        cancellationToken
                    );

                    return AgentResponse.Failed(
                        $"速率限制：请在 {rateLimitResult.RetryAfter?.TotalSeconds:F0} 秒后重试",
                        [],
                        stopwatch.Elapsed
                    );
                }
            }

            // 3. 输入护栏检查
            var processedInput = input;
            if (_guardrailPipeline != null)
            {
                var inputCheckResult = await _guardrailPipeline.CheckInputAsync(
                    input,
                    cancellationToken
                );

                if (!inputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} 输入被护栏阻止: {Reason}",
                        Name,
                        inputCheckResult.Issues.FirstOrDefault()?.Description ?? inputCheckResult.Message ?? "Unknown"
                    );

                    if (_auditLogger != null)
                    {
                        await _auditLogger.LogAsync(
                            new AuditEntry
                            {
                                EventType = AuditEventType.GuardrailTriggered,
                                AgentName = Name,
                                Input = input,
                                ErrorMessage = inputCheckResult.Issues.FirstOrDefault()?.Description ?? inputCheckResult.Message,
                                TriggeredGuardrails = [inputCheckResult.TriggeredBy ?? "InputGuardrail"],
                                Status = AuditResultStatus.Blocked,
                            },
                            cancellationToken
                        );
                    }

                    return AgentResponse.Failed(
                        $"输入未通过安全检查: {inputCheckResult.Issues.FirstOrDefault()?.Description ?? inputCheckResult.Message ?? "内容不合规"}",
                        [],
                        stopwatch.Elapsed
                    );
                }

                processedInput = inputCheckResult.ProcessedContent ?? input;
            }

            // 4. 执行内部 Agent
            var context = new AgentContext { UserInput = processedInput };
            var response = await _innerAgent.RunAsync(context, cancellationToken);

            if (!response.Success)
            {
                await LogAgentEndAsync(
                    response.FinalAnswer,
                    stopwatch.Elapsed,
                    AuditResultStatus.Failed,
                    response.Error,
                    cancellationToken
                );
                return response;
            }

            // 5. 输出护栏检查
            var finalOutput = response.FinalAnswer;
            if (_guardrailPipeline != null && !string.IsNullOrEmpty(finalOutput))
            {
                var outputCheckResult = await _guardrailPipeline.CheckOutputAsync(
                    finalOutput,
                    cancellationToken
                );

                if (!outputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} 输出被护栏阻止: {Reason}",
                        Name,
                        outputCheckResult.Issues.FirstOrDefault()?.Description ?? outputCheckResult.Message ?? "Unknown"
                    );

                    if (_auditLogger != null)
                    {
                        await _auditLogger.LogAsync(
                            new AuditEntry
                            {
                                EventType = AuditEventType.GuardrailTriggered,
                                AgentName = Name,
                                Output = finalOutput,
                                ErrorMessage = outputCheckResult.Issues.FirstOrDefault()?.Description ?? outputCheckResult.Message,
                                TriggeredGuardrails =
                                    [outputCheckResult.TriggeredBy ?? "OutputGuardrail"],
                                Status = AuditResultStatus.Blocked,
                            },
                            cancellationToken
                        );
                    }

                    return AgentResponse.Failed(
                        "输出未通过安全检查，已被过滤",
                        response.Steps,
                        stopwatch.Elapsed
                    );
                }

                // 使用处理后的输出（可能被脱敏）
                if (outputCheckResult.ProcessedContent != finalOutput)
                {
                    finalOutput = outputCheckResult.ProcessedContent;
                }
            }

            // 6. 审计日志 - 结束
            await LogAgentEndAsync(
                finalOutput,
                stopwatch.Elapsed,
                AuditResultStatus.Success,
                null,
                cancellationToken
            );

            // 如果输出被修改，创建新的响应
            if (finalOutput != response.FinalAnswer)
            {
                return AgentResponse.Successful(
                    finalOutput ?? response.FinalAnswer ?? "",
                    response.Steps,
                    stopwatch.Elapsed
                );
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentName} 执行过程中发生错误", Name);

            if (_auditLogger != null)
            {
                await _auditLogger.LogAsync(
                    new AuditEntry
                    {
                        EventType = AuditEventType.Error,
                        AgentName = Name,
                        ErrorMessage = ex.Message,
                        Status = AuditResultStatus.Failed,
                    },
                    cancellationToken
                );
            }

            return AgentResponse.Failed(
                $"执行过程中发生错误: {ex.Message}",
                [],
                stopwatch.Elapsed
            );
        }
    }

    /// <inheritdoc />
    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken: cancellationToken);
    }

    private async Task LogAuditAsync(
        AuditEventType eventType,
        string? sessionId,
        AuditResultStatus status,
        CancellationToken ct
    )
    {
        if (_auditLogger == null)
        {
            return;
        }

        await _auditLogger.LogAsync(
            new AuditEntry
            {
                EventType = eventType,
                AgentName = Name,
                SessionId = sessionId,
                Status = status,
            },
            ct
        );
    }

    private async Task LogAgentEndAsync(
        string? output,
        TimeSpan duration,
        AuditResultStatus status,
        string? errorMessage,
        CancellationToken ct
    )
    {
        if (_auditLogger == null)
        {
            return;
        }

        await _auditLogger.LogAsync(
            new AuditEntry
            {
                EventType = AuditEventType.AgentRunEnd,
                AgentName = Name,
                Output = output,
                DurationMs = (long)duration.TotalMilliseconds,
                Status = status,
                ErrorMessage = errorMessage,
            },
            ct
        );
    }
}
