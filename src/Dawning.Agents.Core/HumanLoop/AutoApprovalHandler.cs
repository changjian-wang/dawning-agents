using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 基于策略的自动审批处理器
/// </summary>
/// <remarks>
/// 适用于：
/// - 测试环境自动批准
/// - 无人值守场景
/// - 低风险操作自动处理
/// </remarks>
public class AutoApprovalHandler : IHumanInteractionHandler
{
    private readonly ILogger<AutoApprovalHandler> _logger;
    private readonly HumanLoopOptions _options;

    /// <summary>
    /// 创建自动审批处理器实例
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
            "自动处理确认请求 {RequestId}，风险等级={RiskLevel}，结果={Result}",
            request.Id,
            request.RiskLevel,
            shouldApprove ? "批准" : "拒绝"
        );

        // 根据确认类型选择合适的响应
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
                Reason = $"自动处理：风险等级 {request.RiskLevel}",
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
        _logger.LogDebug("自动返回输入默认值：{Input}", result);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("通知 [{Level}]：{Message}", level, message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogWarning(
            "收到升级请求 {RequestId}，严重性={Severity}，原因={Reason}",
            request.Id,
            request.Severity,
            request.Reason
        );

        // 对于升级请求，默认跳过
        return Task.FromResult(
            new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Skipped,
                Resolution = "自动跳过升级请求",
            }
        );
    }

    private bool ShouldAutoApprove(ConfirmationRequest request)
    {
        // 根据风险等级和配置决定是否自动批准
        return request.RiskLevel switch
        {
            RiskLevel.Low => true, // 低风险总是批准
            RiskLevel.Medium => !_options.RequireApprovalForMediumRisk,
            RiskLevel.High => false, // 高风险不自动批准
            RiskLevel.Critical => false, // 关键风险不自动批准
            _ => false,
        };
    }

    private static string GetDefaultOption(IReadOnlyList<ConfirmationOption> options)
    {
        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return defaultOpt?.Id ?? (options.Count > 0 ? options[0].Id : "default");
    }
}
