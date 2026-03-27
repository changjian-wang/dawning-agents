using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// Manages approval workflows.
/// </summary>
public class ApprovalWorkflow
{
    private readonly IHumanInteractionHandler _handler;
    private readonly ILogger<ApprovalWorkflow> _logger;
    private readonly ApprovalConfig _config;
    private readonly HumanLoopOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalWorkflow"/> class.
    /// </summary>
    public ApprovalWorkflow(
        IHumanInteractionHandler handler,
        ApprovalConfig config,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<ApprovalWorkflow>? logger = null
    )
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<ApprovalWorkflow>.Instance;
    }

    /// <summary>
    /// Checks whether an action requires approval and requests it.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <param name="description">The action description.</param>
    /// <param name="context">The context data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<ApprovalResult> RequestApprovalAsync(
        string action,
        string description,
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default
    )
    {
        var riskLevel = AssessRiskLevel(action, context);

        // Check whether approval is required based on risk level
        if (!RequiresApproval(riskLevel))
        {
            _logger.LogDebug("Action {Action} auto-approved (risk: {Risk})", action, riskLevel);
            return ApprovalResult.AutoApproved(action);
        }

        _logger.LogInformation(
            "Requesting approval for {Action} (risk: {Risk})",
            action,
            riskLevel
        );

        var request = new ConfirmationRequest
        {
            Type = ConfirmationType.MultiChoice,
            Action = action,
            Description = description,
            RiskLevel = riskLevel,
            Context = context ?? new Dictionary<string, object>(),
            Options =
            [
                new ConfirmationOption
                {
                    Id = "approve",
                    Label = "Approve",
                    IsDefault = true,
                },
                new ConfirmationOption
                {
                    Id = "reject",
                    Label = "Reject",
                    IsDangerous = true,
                },
                new ConfirmationOption { Id = "modify", Label = "Modify" },
            ],
            Timeout = _config.ApprovalTimeout,
            DefaultOnTimeout = _config.DefaultOnTimeout,
        };

        var response = await _handler
            .RequestConfirmationAsync(request, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Received approval response: {SelectedOption}", response.SelectedOption);

        return response.SelectedOption switch
        {
            "approve" => ApprovalResult.Approved(action, response.RespondedBy),
            "reject" => ApprovalResult.Rejected(action, response.Reason, response.RespondedBy),
            "modify" => ApprovalResult.Modified(
                action,
                response.ModifiedContent,
                response.RespondedBy
            ),
            "timeout" => _config.DefaultOnTimeout == "approve"
                ? ApprovalResult.AutoApproved(action)
                : ApprovalResult.TimedOut(action),
            _ => ApprovalResult.Rejected(action, "Unknown response"),
        };
    }

    /// <summary>
    /// Requests multi-party approval.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <param name="description">The action description.</param>
    /// <param name="requiredApprovals">The number of required approvals.</param>
    /// <param name="context">The context data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The approval result.</returns>
    public async Task<ApprovalResult> RequestMultiApprovalAsync(
        string action,
        string description,
        int requiredApprovals,
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default
    )
    {
        var approvals = new List<string>();
        var rejections = new List<(string Approver, string Reason)>();

        _logger.LogInformation(
            "Requesting multi-party approval for {Action} (requires {Required} approvers)",
            action,
            requiredApprovals
        );

        for (int i = 0; i < requiredApprovals; i++)
        {
            var result = await RequestApprovalAsync(
                    $"{action} (approval {i + 1}/{requiredApprovals})",
                    description,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (result.IsApproved)
            {
                approvals.Add(result.ApprovedBy ?? $"Approver-{i + 1}");
                _logger.LogDebug("Received approval {Number}", i + 1);
            }
            else
            {
                rejections.Add(
                    (result.ApprovedBy ?? $"Approver-{i + 1}", result.RejectionReason ?? "Unknown")
                );
                _logger.LogDebug("Received rejection: {Reason}", result.RejectionReason);
            }
        }

        if (approvals.Count >= requiredApprovals)
        {
            _logger.LogInformation(
                "Multi-party approval granted: {Approvers}",
                string.Join(", ", approvals)
            );
            return ApprovalResult.Approved(action, string.Join(", ", approvals));
        }

        var rejectionSummary = string.Join(
            "; ",
            rejections.Select(r => $"{r.Approver}: {r.Reason}")
        );
        _logger.LogWarning(
            "Multi-party approval denied: {Approved}/{Required}. Rejections: {Rejections}",
            approvals.Count,
            requiredApprovals,
            rejectionSummary
        );

        return ApprovalResult.Rejected(
            action,
            $"Insufficient approvals: {approvals.Count}/{requiredApprovals}. Rejections: {rejectionSummary}"
        );
    }

    /// <summary>
    /// Assesses the risk level of an action.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <param name="context">The context data.</param>
    /// <returns>The risk level.</returns>
    public RiskLevel AssessRiskLevel(string action, IReadOnlyDictionary<string, object>? context)
    {
        // Check critical risk keywords
        if (
            _options.CriticalRiskKeywords.Any(k =>
                action.Contains(k, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return RiskLevel.Critical;
        }

        // Check high risk keywords
        if (
            _options.HighRiskKeywords.Any(k =>
                action.Contains(k, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return RiskLevel.High;
        }

        // Check risk indicators in context
        if (context != null)
        {
            if (context.TryGetValue("amount", out var amount) && amount is IConvertible)
            {
                try
                {
                    var d = Convert.ToDecimal(
                        amount,
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                    if (d > 10000)
                    {
                        return RiskLevel.High;
                    }
                }
                catch (FormatException) { }
                catch (OverflowException) { }
            }

            if (
                context.TryGetValue("environment", out var env)
                && env?.ToString()?.Equals("production", StringComparison.OrdinalIgnoreCase) == true
            )
            {
                return RiskLevel.Critical;
            }

            if (
                context.TryGetValue("riskLevel", out var explicitRisk)
                && Enum.TryParse<RiskLevel>(explicitRisk?.ToString(), out var parsed)
            )
            {
                return parsed;
            }
        }

        return RiskLevel.Medium;
    }

    /// <summary>
    /// Checks whether the specified risk level requires approval.
    /// </summary>
    private bool RequiresApproval(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => _config.RequireApprovalForLowRisk,
            RiskLevel.Medium => _config.RequireApprovalForMediumRisk,
            RiskLevel.High => true,
            RiskLevel.Critical => true,
            _ => true,
        };
    }
}
