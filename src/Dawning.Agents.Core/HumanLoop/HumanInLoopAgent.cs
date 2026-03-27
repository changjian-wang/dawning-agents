using System.Diagnostics;
using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// An agent wrapper that introduces human participation at decision points.
/// </summary>
public class HumanInLoopAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly IHumanInteractionHandler _handler;
    private readonly ApprovalWorkflow _workflow;
    private readonly HumanLoopOptions _options;
    private readonly ILogger<HumanInLoopAgent> _logger;

    /// <inheritdoc />
    public string Name => $"HumanLoop({_innerAgent.Name})";

    /// <inheritdoc />
    public string Instructions => _innerAgent.Instructions;

    /// <summary>
    /// Initializes a new instance of the <see cref="HumanInLoopAgent"/> class.
    /// </summary>
    public HumanInLoopAgent(
        IAgent innerAgent,
        IHumanInteractionHandler handler,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<HumanInLoopAgent>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(innerAgent);
        ArgumentNullException.ThrowIfNull(handler);
        _innerAgent = innerAgent;
        _handler = handler;
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<HumanInLoopAgent>.Instance;
        _workflow = new ApprovalWorkflow(
            handler,
            new ApprovalConfig
            {
                RequireApprovalForMediumRisk = _options.RequireApprovalForMediumRisk,
                ApprovalTimeout = _options.DefaultTimeout,
            },
            options
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HumanInLoopAgent"/> class with a custom workflow.
    /// </summary>
    public HumanInLoopAgent(
        IAgent innerAgent,
        IHumanInteractionHandler handler,
        ApprovalWorkflow workflow,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<HumanInLoopAgent>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(innerAgent);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(workflow);
        _innerAgent = innerAgent;
        _handler = handler;
        _workflow = workflow;
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<HumanInLoopAgent>.Instance;
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var context = new AgentContext { UserInput = input };
        return await RunAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Pre-execution confirmation (if configured)
            if (_options.ConfirmBeforeExecution)
            {
                var approval = await _workflow
                    .RequestApprovalAsync(
                        "Execute agent task",
                        $"Agent '{_innerAgent.Name}' will process: {context.UserInput}",
                        new Dictionary<string, object> { ["sessionId"] = context.SessionId },
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                if (!approval.IsApproved)
                {
                    _logger.LogWarning("Task not approved: {Reason}", approval.RejectionReason);
                    return AgentResponse.Failed(
                        $"Task not approved: {approval.RejectionReason}",
                        [],
                        stopwatch.Elapsed
                    );
                }

                _logger.LogInformation("Task approved");
            }

            // Execute with escalation handling
            var response = await ExecuteWithEscalationAsync(context, cancellationToken)
                .ConfigureAwait(false);

            // Pre-return review (if configured)
            if (_options.ReviewBeforeReturn && response.Success)
            {
                response = await ReviewResponseAsync(response, stopwatch.Elapsed, cancellationToken)
                    .ConfigureAwait(false);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AgentEscalationException ex)
        {
            _logger.LogWarning("Agent escalation: {Reason}", ex.Reason);

            var escalation = await _handler
                .EscalateAsync(
                    new EscalationRequest
                    {
                        Reason = ex.Reason,
                        Description = ex.Description,
                        Severity = EscalationSeverity.High,
                        AgentName = _innerAgent.Name,
                        TaskId = context.SessionId,
                        Context = ex.Context,
                        AttemptedSolutions = ex.AttemptedSolutions,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            return HandleEscalationResult(escalation, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during agent execution");
            return AgentResponse.Failed(
                $"Error occurred during execution: {ex.Message}",
                [],
                stopwatch.Elapsed,
                ex
            );
        }
    }

    /// <summary>
    /// Executes with escalation handling.
    /// </summary>
    private async Task<AgentResponse> ExecuteWithEscalationAsync(
        AgentContext context,
        CancellationToken cancellationToken
    )
    {
        Exception? lastException = null;
        var attemptedSolutions = new List<string>();

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Executing attempt {Attempt}", attempt + 1);
                return await _innerAgent.RunAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (AgentEscalationException)
            {
                // AgentEscalationException should propagate directly
                throw;
            }
            catch (OperationCanceledException)
            {
                // Cancellation should propagate directly
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attemptedSolutions.Add($"Attempt {attempt + 1} failed: {ex.Message}");
                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt} failed, requesting guidance",
                    attempt + 1
                );

                // Last retry failed, exit loop so subsequent code throws escalation exception
                if (attempt >= _options.MaxRetries)
                {
                    break;
                }

                var input = await _handler
                    .RequestInputAsync(
                        $"Agent encountered an error: {ex.Message}\nPlease provide guidance or enter 'abort' to stop:",
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);

                if (input.Equals("abort", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("User chose to abort operation");
                    throw new OperationCanceledException("User aborted");
                }

                // Retry with guidance - save old metadata and create new context
                var oldMetadata = context.Metadata;
                context = new AgentContext
                {
                    SessionId = context.SessionId,
                    UserInput = $"{context.UserInput}\n\nAdditional guidance: {input}",
                    MaxSteps = context.MaxSteps,
                };

                // Copy metadata
                foreach (var kvp in oldMetadata)
                {
                    context.SetMetadata(kvp.Key, kvp.Value);
                }
            }
        }

        // All retries failed, throw escalation exception
        if (lastException != null)
        {
            throw new AgentEscalationException(
                "Failed after multiple attempts",
                lastException.Message,
                lastException,
                new Dictionary<string, object>
                {
                    ["attempts"] = _options.MaxRetries + 1,
                    ["lastError"] = lastException.Message,
                },
                attemptedSolutions
            );
        }
        else
        {
            throw new AgentEscalationException(
                "Failed after multiple attempts",
                "Unknown error",
                new Dictionary<string, object> { ["attempts"] = _options.MaxRetries + 1 },
                attemptedSolutions
            );
        }
    }

    /// <summary>
    /// Reviews the response.
    /// </summary>
    private async Task<AgentResponse> ReviewResponseAsync(
        AgentResponse response,
        TimeSpan currentDuration,
        CancellationToken cancellationToken
    )
    {
        var review = await _handler
            .RequestConfirmationAsync(
                new ConfirmationRequest
                {
                    Type = ConfirmationType.Review,
                    Action = "Review response",
                    Description = $"Agent response:\n\n{response.FinalAnswer}",
                    RiskLevel = RiskLevel.Low,
                    Options =
                    [
                        new ConfirmationOption
                        {
                            Id = "approve",
                            Label = "Approve",
                            IsDefault = true,
                        },
                        new ConfirmationOption { Id = "edit", Label = "Edit response" },
                        new ConfirmationOption { Id = "reject", Label = "Reject" },
                    ],
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return review.SelectedOption switch
        {
            "approve" => response,
            "edit" => response with
            {
                FinalAnswer = review.ModifiedContent ?? response.FinalAnswer,
            },
            "reject" => AgentResponse.Failed(
                "Response rejected by reviewer",
                response.Steps,
                currentDuration
            ),
            _ => response,
        };
    }

    /// <summary>
    /// Handles the escalation result.
    /// </summary>
    private AgentResponse HandleEscalationResult(EscalationResult result, TimeSpan duration)
    {
        return result.Action switch
        {
            EscalationAction.Resolved => AgentResponse.Successful(
                result.Resolution ?? "Resolved by human",
                [],
                duration
            ),
            EscalationAction.Skipped => AgentResponse.Successful(
                "Step skipped by human",
                [],
                duration
            ),
            _ => AgentResponse.Failed("Operation aborted by human", [], duration),
        };
    }
}
