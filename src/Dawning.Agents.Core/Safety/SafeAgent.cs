using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// 安全 Agent 包装器 - 为 Agent 添加护栏、速率限制和审计功能
/// </summary>
public sealed class SafeAgent : IAgent
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
    public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        return RunAsync(input, userId: null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 执行 Agent（带用户 ID）
    /// </summary>
    public Task<AgentResponse> RunAsync(
        string input,
        string? userId = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input };
        if (userId != null)
        {
            context.SetMetadata("userId", userId);
        }

        return RunAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var userId = context.Metadata.TryGetValue("userId", out var uid) ? uid?.ToString() : null;
        var rateLimitKey = userId ?? $"anonymous:{Name}";

        try
        {
            // 1. 审计日志 - 开始
            if (_auditLogger != null)
            {
                await _auditLogger
                    .LogAsync(
                        new AuditEntry
                        {
                            EventType = AuditEventType.AgentRunStart,
                            AgentName = Name,
                            SessionId = rateLimitKey,
                            Input = context.UserInput,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            // 2. 速率限制检查
            if (_rateLimiter != null)
            {
                var rateLimitResult = await _rateLimiter
                    .TryAcquireAsync(rateLimitKey, cancellationToken)
                    .ConfigureAwait(false);

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
                        )
                        .ConfigureAwait(false);

                    return AgentResponse.Failed(
                        $"速率限制：请在 {rateLimitResult.RetryAfter?.TotalSeconds:F0} 秒后重试",
                        [],
                        stopwatch.Elapsed
                    );
                }
            }

            // 3. 输入护栏检查
            if (_guardrailPipeline != null)
            {
                var inputCheckResult = await _guardrailPipeline
                    .CheckInputAsync(context.UserInput, cancellationToken)
                    .ConfigureAwait(false);

                if (!inputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} 输入被护栏阻止: {Reason}",
                        Name,
                        inputCheckResult.Issues.FirstOrDefault()?.Description
                            ?? inputCheckResult.Message
                            ?? "Unknown"
                    );

                    if (_auditLogger != null)
                    {
                        await _auditLogger
                            .LogAsync(
                                new AuditEntry
                                {
                                    EventType = AuditEventType.GuardrailTriggered,
                                    AgentName = Name,
                                    Input = context.UserInput,
                                    ErrorMessage =
                                        inputCheckResult.Issues.FirstOrDefault()?.Description
                                        ?? inputCheckResult.Message,
                                    TriggeredGuardrails =
                                    [
                                        inputCheckResult.TriggeredBy ?? "InputGuardrail",
                                    ],
                                    Status = AuditResultStatus.Blocked,
                                },
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                    }

                    return AgentResponse.Failed(
                        $"输入未通过安全检查: {inputCheckResult.Issues.FirstOrDefault()?.Description ?? inputCheckResult.Message ?? "内容不合规"}",
                        [],
                        stopwatch.Elapsed
                    );
                }

                // 应用处理后的输入（可能被脱敏）
                if (
                    inputCheckResult.ProcessedContent != null
                    && inputCheckResult.ProcessedContent != context.UserInput
                )
                {
                    var processedContext = new AgentContext
                    {
                        UserInput = inputCheckResult.ProcessedContent,
                        SessionId = context.SessionId,
                        MaxSteps = context.MaxSteps,
                    };
                    foreach (var kvp in context.Metadata)
                    {
                        processedContext.SetMetadata(kvp.Key, kvp.Value);
                    }

                    context = processedContext;
                }
            }

            // 4. 执行内部 Agent（保留原始上下文）
            var response = await _innerAgent
                .RunAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (!response.Success)
            {
                await LogAgentEndAsync(
                        response.FinalAnswer,
                        stopwatch.Elapsed,
                        AuditResultStatus.Failed,
                        response.Error,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                return response;
            }

            // 5. 输出护栏检查
            var finalOutput = response.FinalAnswer;
            if (_guardrailPipeline != null && !string.IsNullOrEmpty(finalOutput))
            {
                var outputCheckResult = await _guardrailPipeline
                    .CheckOutputAsync(finalOutput, cancellationToken)
                    .ConfigureAwait(false);

                if (!outputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} 输出被护栏阻止: {Reason}",
                        Name,
                        outputCheckResult.Issues.FirstOrDefault()?.Description
                            ?? outputCheckResult.Message
                            ?? "Unknown"
                    );

                    if (_auditLogger != null)
                    {
                        await _auditLogger
                            .LogAsync(
                                new AuditEntry
                                {
                                    EventType = AuditEventType.GuardrailTriggered,
                                    AgentName = Name,
                                    Output = finalOutput,
                                    ErrorMessage =
                                        outputCheckResult.Issues.FirstOrDefault()?.Description
                                        ?? outputCheckResult.Message,
                                    TriggeredGuardrails =
                                    [
                                        outputCheckResult.TriggeredBy ?? "OutputGuardrail",
                                    ],
                                    Status = AuditResultStatus.Blocked,
                                },
                                cancellationToken
                            )
                            .ConfigureAwait(false);
                    }

                    return AgentResponse.Failed(
                        $"输出未通过安全检查: {outputCheckResult.Issues.FirstOrDefault()?.Description ?? outputCheckResult.Message ?? "内容不合规"}",
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
                )
                .ConfigureAwait(false);

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
            _logger.LogError(ex, "Agent {AgentName} 执行异常", Name);

            if (_auditLogger != null)
            {
                await _auditLogger
                    .LogAsync(
                        new AuditEntry
                        {
                            EventType = AuditEventType.Error,
                            AgentName = Name,
                            ErrorMessage = ex.Message,
                            Status = AuditResultStatus.Failed,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            return AgentResponse.Failed(
                $"执行过程中发生错误: {ex.Message}",
                [],
                stopwatch.Elapsed,
                ex
            );
        }
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

        await _auditLogger
            .LogAsync(
                new AuditEntry
                {
                    EventType = eventType,
                    AgentName = Name,
                    SessionId = sessionId,
                    Status = status,
                },
                ct
            )
            .ConfigureAwait(false);
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

        await _auditLogger
            .LogAsync(
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
            )
            .ConfigureAwait(false);
    }
}
