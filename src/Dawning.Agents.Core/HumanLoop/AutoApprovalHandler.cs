using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// Policy-based auto-approval handler.
/// </summary>
/// <remarks>
/// Suitable for:
/// - Auto-approving in test environments
/// - Unattended scenarios
/// - Automatic processing of low-risk operations
/// </remarks>
public class AutoApprovalHandler : IHumanInteractionHandler
{
    private readonly ILogger<AutoApprovalHandler> _logger;
    private readonly HumanLoopOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoApprovalHandler"/> class.
    /// </summary>
    public AutoApprovalHandler(
        IOptions<HumanLoopOptions>? options = null,
        ILogger<AutoApprovalHandler>? logger = null
    )
    {
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<AutoApprovalHandler>.Instance;
    }

    /// <inheritdoc />
    public Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var shouldApprove = ShouldAutoApprove(request);

        _logger.LogDebug(
            "Auto-processing confirmation request {RequestId}, RiskLevel={RiskLevel}, Result={Result}",
            request.Id,
            request.RiskLevel,
            shouldApprove ? "Approved" : "Rejected"
        );

        // Select appropriate response based on confirmation type
        var selectedOption = request.Type switch
        {
            ConfirmationType.Binary => shouldApprove ? "yes" : "no",
            ConfirmationType.MultiChoice => GetDefaultOption(request.Options),
            ConfirmationType.FreeformInput => "auto-approved",
            ConfirmationType.Review => shouldApprove ? "approve" : "reject",
            _ => shouldApprove ? "yes" : "no",
        };

        return Task.FromResult(
            new ConfirmationResponse
            {
                RequestId = request.Id,
                SelectedOption = selectedOption,
                Reason = $"Auto-processed: risk level {request.RiskLevel}",
            }
        );
    }

    /// <inheritdoc />
    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = defaultValue ?? "";
        _logger.LogDebug("Auto-returning default input: {Input}", result);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Notification [{Level}]: {Message}", level, message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogWarning(
            "Received escalation request {RequestId}, Severity={Severity}, Reason={Reason}",
            request.Id,
            request.Severity,
            request.Reason
        );

        // Skip escalation requests by default
        return Task.FromResult(
            new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Skipped,
                Resolution = "Auto-skipped escalation request",
            }
        );
    }

    private bool ShouldAutoApprove(ConfirmationRequest request)
    {
        // Determine auto-approval based on risk level and configuration
        return request.RiskLevel switch
        {
            RiskLevel.Low => true, // Low risk: always approve
            RiskLevel.Medium => !_options.RequireApprovalForMediumRisk,
            RiskLevel.High => false, // High risk: never auto-approve
            RiskLevel.Critical => false, // Critical risk: never auto-approve
            _ => false,
        };
    }

    private static string GetDefaultOption(IReadOnlyList<ConfirmationOption> options)
    {
        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return defaultOpt?.Id ?? (options.Count > 0 ? options[0].Id : "default");
    }
}
