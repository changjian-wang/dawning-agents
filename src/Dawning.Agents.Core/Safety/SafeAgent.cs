using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Safety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.Safety;

/// <summary>
/// Safety agent wrapper that adds guardrails, rate limiting, and audit logging to an agent.
/// </summary>
public sealed class SafeAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly IGuardrailPipeline? _guardrailPipeline;
    private readonly IRateLimiter? _rateLimiter;
    private readonly ITokenRateLimiter? _tokenRateLimiter;
    private readonly IAuditLogger? _auditLogger;
    private readonly ILogger<SafeAgent> _logger;

    public SafeAgent(
        IAgent innerAgent,
        IGuardrailPipeline? guardrailPipeline = null,
        IRateLimiter? rateLimiter = null,
        ITokenRateLimiter? tokenRateLimiter = null,
        IAuditLogger? auditLogger = null,
        ILogger<SafeAgent>? logger = null
    )
    {
        _innerAgent = innerAgent ?? throw new ArgumentNullException(nameof(innerAgent));
        _guardrailPipeline = guardrailPipeline;
        _rateLimiter = rateLimiter;
        _tokenRateLimiter = tokenRateLimiter;
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
    /// Runs the agent with a user ID.
    /// </summary>
    public Task<AgentResponse> RunAsync(
        string input,
        string? userId = null,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input, UserId = userId };

        return RunAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var rateLimitKey = context.UserId ?? $"anonymous:{Name}";

        try
        {
            // 1. Audit log - start
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

            // 2. Rate limit check
            if (_rateLimiter != null)
            {
                var rateLimitResult = await _rateLimiter
                    .TryAcquireAsync(rateLimitKey, cancellationToken)
                    .ConfigureAwait(false);

                if (!rateLimitResult.IsAllowed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} rate limited, retry after {RetryAfter}",
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
                        $"Rate limited: please retry after {rateLimitResult.RetryAfter?.TotalSeconds:F0} seconds",
                        [],
                        stopwatch.Elapsed
                    );
                }
            }

            // 2.5 Token budget check
            if (_tokenRateLimiter != null)
            {
                if (!_tokenRateLimiter.HasBudget(rateLimitKey))
                {
                    var usedTokens = _tokenRateLimiter.GetUsedTokens(rateLimitKey);
                    _logger.LogWarning(
                        "Agent {AgentName} token budget exhausted: SessionId={SessionId}, UsedTokens={UsedTokens}",
                        Name,
                        rateLimitKey,
                        usedTokens
                    );

                    await LogAuditAsync(
                            AuditEventType.RateLimited,
                            rateLimitKey,
                            AuditResultStatus.RateLimited,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    return AgentResponse.Failed(
                        "Token budget exhausted. Contact an administrator or wait for session reset.",
                        [],
                        stopwatch.Elapsed
                    );
                }
            }

            // 3. Input guardrail check
            if (_guardrailPipeline != null)
            {
                var inputCheckResult = await _guardrailPipeline
                    .CheckInputAsync(context.UserInput, cancellationToken)
                    .ConfigureAwait(false);

                if (!inputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} input blocked by guardrail: {Reason}",
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
                        $"Input failed safety check: {inputCheckResult.Issues.FirstOrDefault()?.Description ?? inputCheckResult.Message ?? "Non-compliant content"}",
                        [],
                        stopwatch.Elapsed
                    );
                }

                // Apply processed input (may have been redacted)
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

            // 4. Execute inner agent (preserve original context)
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

            // 5. Output guardrail check
            var finalOutput = response.FinalAnswer;
            if (_guardrailPipeline != null && !string.IsNullOrEmpty(finalOutput))
            {
                var outputCheckResult = await _guardrailPipeline
                    .CheckOutputAsync(finalOutput, cancellationToken)
                    .ConfigureAwait(false);

                if (!outputCheckResult.Passed)
                {
                    _logger.LogWarning(
                        "Agent {AgentName} output blocked by guardrail: {Reason}",
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
                        $"Output failed safety check: {outputCheckResult.Issues.FirstOrDefault()?.Description ?? outputCheckResult.Message ?? "Non-compliant content"}",
                        response.Steps,
                        stopwatch.Elapsed
                    );
                }

                // Use processed output (may have been redacted)
                if (outputCheckResult.ProcessedContent != finalOutput)
                {
                    finalOutput = outputCheckResult.ProcessedContent;
                }
            }

            // 6. Audit log - end
            await LogAgentEndAsync(
                    finalOutput,
                    stopwatch.Elapsed,
                    AuditResultStatus.Success,
                    null,
                    cancellationToken
                )
                .ConfigureAwait(false);

            // If output was modified, create a new response
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent {AgentName} execution exception", Name);

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
                $"An error occurred during execution: {ex.Message}",
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
