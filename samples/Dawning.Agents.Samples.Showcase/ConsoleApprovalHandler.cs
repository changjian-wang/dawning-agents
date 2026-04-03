using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Samples.Common;

namespace Dawning.Agents.Samples.Showcase;

/// <summary>
/// 控制台审批处理器 — 在控制台请求用户确认
/// </summary>
public class ConsoleApprovalHandler : IHumanInteractionHandler
{
    public Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Console.Write($"  确认 [{request.Action}]: {request.Description}? (Y/n): ");
        var input = Console.ReadLine()?.Trim().ToUpperInvariant();
        var confirmed = input != "N";

        return Task.FromResult(
            new ConfirmationResponse
            {
                RequestId = request.Id,
                SelectedOption = confirmed ? "yes" : "no",
                RespondedBy = "Console User",
                Reason = confirmed ? null : "用户拒绝",
                RespondedAt = DateTime.UtcNow,
            }
        );
    }

    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        Console.Write($"  {prompt} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return Task.FromResult(string.IsNullOrEmpty(input) ? defaultValue ?? "" : input);
    }

    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        var color = level switch
        {
            NotificationLevel.Warning => ConsoleColor.Yellow,
            NotificationLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.Cyan,
        };

        ConsoleHelper.PrintColored($"  通知: {message}", color);
        return Task.CompletedTask;
    }

    public Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ConsoleHelper.PrintColored(
            $"  ⚠️ 升级请求: {request.Reason} — {request.Description}",
            ConsoleColor.Yellow
        );
        return Task.FromResult(
            new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Resolved,
                Resolution = "Console user acknowledged",
                ResolvedBy = "Console User",
            }
        );
    }
}
