using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 审批工作流管理器
/// </summary>
public class ApprovalWorkflow
{
    private readonly IHumanInteractionHandler _handler;
    private readonly ILogger<ApprovalWorkflow> _logger;
    private readonly ApprovalConfig _config;
    private readonly HumanLoopOptions _options;

    /// <summary>
    /// 创建审批工作流实例
    /// </summary>
    public ApprovalWorkflow(
        IHumanInteractionHandler handler,
        ApprovalConfig config,
        IOptions<HumanLoopOptions>? options = null,
        ILogger<ApprovalWorkflow>? logger = null
    )
    {
        _handler = handler;
        _config = config;
        _options = options?.Value ?? new HumanLoopOptions();
        _logger = logger ?? NullLogger<ApprovalWorkflow>.Instance;
    }

    /// <summary>
    /// 检查操作是否需要审批并获取审批
    /// </summary>
    /// <param name="action">操作名称</param>
    /// <param name="description">操作描述</param>
    /// <param name="context">上下文数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批结果</returns>
    public async Task<ApprovalResult> RequestApprovalAsync(
        string action,
        string description,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default
    )
    {
        var riskLevel = AssessRiskLevel(action, context);

        // 根据风险级别检查是否需要审批
        if (!RequiresApproval(riskLevel))
        {
            _logger.LogDebug("操作 {Action} 自动批准（风险：{Risk}）", action, riskLevel);
            return ApprovalResult.AutoApproved(action);
        }

        _logger.LogInformation("请求审批 {Action}（风险：{Risk}）", action, riskLevel);

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
                    Label = "批准",
                    IsDefault = true,
                },
                new ConfirmationOption
                {
                    Id = "reject",
                    Label = "拒绝",
                    IsDangerous = true,
                },
                new ConfirmationOption { Id = "modify", Label = "修改" },
            ],
            Timeout = _config.ApprovalTimeout,
            DefaultOnTimeout = _config.DefaultOnTimeout,
        };

        var response = await _handler.RequestConfirmationAsync(request, cancellationToken);

        _logger.LogDebug("收到审批响应：{SelectedOption}", response.SelectedOption);

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
            _ => ApprovalResult.Rejected(action, "未知响应"),
        };
    }

    /// <summary>
    /// 请求多人审批
    /// </summary>
    /// <param name="action">操作名称</param>
    /// <param name="description">操作描述</param>
    /// <param name="requiredApprovals">需要的审批数量</param>
    /// <param name="context">上下文数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批结果</returns>
    public async Task<ApprovalResult> RequestMultiApprovalAsync(
        string action,
        string description,
        int requiredApprovals,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default
    )
    {
        var approvals = new List<string>();
        var rejections = new List<(string Approver, string Reason)>();

        _logger.LogInformation(
            "请求多人审批 {Action}（需要 {Required} 人批准）",
            action,
            requiredApprovals
        );

        for (int i = 0; i < requiredApprovals; i++)
        {
            var result = await RequestApprovalAsync(
                $"{action}（审批 {i + 1}/{requiredApprovals}）",
                description,
                context,
                cancellationToken
            );

            if (result.IsApproved)
            {
                approvals.Add(result.ApprovedBy ?? $"审批人-{i + 1}");
                _logger.LogDebug("收到第 {Number} 个批准", i + 1);
            }
            else
            {
                rejections.Add(
                    (result.ApprovedBy ?? $"审批人-{i + 1}", result.RejectionReason ?? "未知")
                );
                _logger.LogDebug("收到拒绝：{Reason}", result.RejectionReason);
            }
        }

        if (approvals.Count >= requiredApprovals)
        {
            _logger.LogInformation("多人审批通过：{Approvers}", string.Join(", ", approvals));
            return ApprovalResult.Approved(action, string.Join(", ", approvals));
        }

        var rejectionSummary = string.Join(
            "；",
            rejections.Select(r => $"{r.Approver}：{r.Reason}")
        );
        _logger.LogWarning(
            "多人审批未通过：{Approved}/{Required}。拒绝：{Rejections}",
            approvals.Count,
            requiredApprovals,
            rejectionSummary
        );

        return ApprovalResult.Rejected(
            action,
            $"审批数量不足：{approvals.Count}/{requiredApprovals}。拒绝：{rejectionSummary}"
        );
    }

    /// <summary>
    /// 评估操作的风险级别
    /// </summary>
    /// <param name="action">操作名称</param>
    /// <param name="context">上下文数据</param>
    /// <returns>风险级别</returns>
    public RiskLevel AssessRiskLevel(string action, IDictionary<string, object>? context)
    {
        var lowerAction = action.ToLower();

        // 检查关键风险关键词
        if (_options.CriticalRiskKeywords.Any(k => lowerAction.Contains(k.ToLower())))
        {
            return RiskLevel.Critical;
        }

        // 检查高风险关键词
        if (_options.HighRiskKeywords.Any(k => lowerAction.Contains(k.ToLower())))
        {
            return RiskLevel.High;
        }

        // 检查上下文中的风险指标
        if (context != null)
        {
            if (context.TryGetValue("amount", out var amount) && amount is decimal d && d > 10000)
            {
                return RiskLevel.High;
            }

            if (
                context.TryGetValue("environment", out var env)
                && env?.ToString()?.ToLower() == "production"
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
    /// 检查指定风险级别是否需要审批
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
