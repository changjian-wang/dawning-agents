using Dawning.Agents.Abstractions.HumanLoop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dawning.Agents.Core.HumanLoop;

/// <summary>
/// 基于控制台的人机交互处理器
/// </summary>
public class ConsoleInteractionHandler : IHumanInteractionHandler
{
    private readonly ILogger<ConsoleInteractionHandler> _logger;

    /// <summary>
    /// 创建控制台交互处理器实例
    /// </summary>
    public ConsoleInteractionHandler(ILogger<ConsoleInteractionHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<ConsoleInteractionHandler>.Instance;
    }

    /// <inheritdoc />
    public async Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        PrintHeader("🔔 需要确认", request.RiskLevel.ToString());
        Console.WriteLine($"操作：{request.Action}");
        Console.WriteLine($"描述：{request.Description}");
        Console.WriteLine();

        if (request.Context.Count > 0)
        {
            Console.WriteLine("上下文：");
            foreach (var (key, value) in request.Context)
            {
                Console.WriteLine($"  {key}：{value}");
            }
            Console.WriteLine();
        }

        _logger.LogDebug("等待用户确认请求 {RequestId}", request.Id);

        string selectedOption;
        string? freeformInput = null;
        string? modifiedContent = null;

        switch (request.Type)
        {
            case ConfirmationType.Binary:
                selectedOption = await GetBinaryConfirmationAsync(cancellationToken);
                break;

            case ConfirmationType.MultiChoice:
                selectedOption = await GetMultiChoiceConfirmationAsync(
                    request.Options,
                    cancellationToken
                );
                break;

            case ConfirmationType.FreeformInput:
                freeformInput = await GetFreeformInputAsync(cancellationToken);
                selectedOption = "input";
                break;

            case ConfirmationType.Review:
                (selectedOption, modifiedContent) = await GetReviewConfirmationAsync(
                    request.Description,
                    request.Options,
                    cancellationToken
                );
                break;

            default:
                selectedOption = "unknown";
                break;
        }

        _logger.LogDebug("用户选择：{SelectedOption}", selectedOption);

        return new ConfirmationResponse
        {
            RequestId = request.Id,
            SelectedOption = selectedOption,
            FreeformInput = freeformInput,
            ModifiedContent = modifiedContent,
        };
    }

    /// <inheritdoc />
    public Task<string> RequestInputAsync(
        string prompt,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        Console.WriteLine();
        Console.Write($"📝 {prompt}");
        if (defaultValue != null)
        {
            Console.Write($" [{defaultValue}]");
        }
        Console.Write("：");

        var input = Console.ReadLine();
        var result = string.IsNullOrWhiteSpace(input) ? (defaultValue ?? "") : input;

        _logger.LogDebug("用户输入：{Input}", result);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task NotifyAsync(
        string message,
        NotificationLevel level = NotificationLevel.Info,
        CancellationToken cancellationToken = default
    )
    {
        var icon = level switch
        {
            NotificationLevel.Info => "ℹ️",
            NotificationLevel.Warning => "⚠️",
            NotificationLevel.Error => "❌",
            NotificationLevel.Success => "✅",
            _ => "📢",
        };

        Console.WriteLine($"{icon} {message}");
        _logger.LogDebug("通知已发送：{Level} - {Message}", level, message);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<EscalationResult> EscalateAsync(
        EscalationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        PrintHeader("🚨 需要升级处理", request.Severity.ToString());
        Console.WriteLine($"原因：{request.Reason}");
        Console.WriteLine($"描述：{request.Description}");
        Console.WriteLine();

        if (request.AttemptedSolutions.Count > 0)
        {
            Console.WriteLine("已尝试的解决方案：");
            foreach (var solution in request.AttemptedSolutions)
            {
                Console.WriteLine($"  - {solution}");
            }
            Console.WriteLine();
        }

        if (request.Context.Count > 0)
        {
            Console.WriteLine("上下文：");
            foreach (var (key, value) in request.Context)
            {
                Console.WriteLine($"  {key}：{value}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("可用操作：");
        Console.WriteLine("  1. 解决 - 提供解决方案");
        Console.WriteLine("  2. 跳过 - 跳过此操作");
        Console.WriteLine("  3. 中止 - 中止整个操作");
        Console.WriteLine();

        Console.Write("选择操作 (1/2/3)：");
        var choice = Console.ReadLine()?.Trim();

        _logger.LogDebug("用户选择升级操作：{Choice}", choice);

        return choice switch
        {
            "1" => new EscalationResult
            {
                RequestId = request.Id,
                Action = EscalationAction.Resolved,
                Resolution = await RequestInputAsync(
                    "输入解决方案",
                    cancellationToken: cancellationToken
                ),
            },
            "2" => new EscalationResult { RequestId = request.Id, Action = EscalationAction.Skipped },
            _ => new EscalationResult { RequestId = request.Id, Action = EscalationAction.Aborted },
        };
    }

    private static void PrintHeader(string title, string? subtitle = null)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        if (subtitle != null)
        {
            Console.WriteLine($"{title} ({subtitle})");
        }
        else
        {
            Console.WriteLine(title);
        }
        Console.WriteLine("═══════════════════════════════════════════");
    }

    private static Task<string> GetBinaryConfirmationAsync(CancellationToken cancellationToken)
    {
        Console.Write("继续？(y/n)：");
        var input = Console.ReadLine()?.Trim().ToLower();
        return Task.FromResult(input == "y" || input == "yes" ? "yes" : "no");
    }

    private static Task<string> GetMultiChoiceConfirmationAsync(
        IReadOnlyList<ConfirmationOption> options,
        CancellationToken cancellationToken
    )
    {
        Console.WriteLine("选项：");
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var marker = opt.IsDefault ? "*" : " ";
            var danger = opt.IsDangerous ? " ⚠️" : "";
            Console.WriteLine($"  {marker}{i + 1}. {opt.Label}{danger}");
            if (!string.IsNullOrEmpty(opt.Description))
            {
                Console.WriteLine($"      {opt.Description}");
            }
        }
        Console.WriteLine();

        Console.Write("选择选项：");
        var input = Console.ReadLine()?.Trim();

        if (int.TryParse(input, out var index) && index > 0 && index <= options.Count)
        {
            return Task.FromResult(options[index - 1].Id);
        }

        // 返回默认选项
        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return Task.FromResult(defaultOpt?.Id ?? (options.Count > 0 ? options[0].Id : "unknown"));
    }

    private static Task<string> GetFreeformInputAsync(CancellationToken cancellationToken)
    {
        Console.Write("输入您的内容：");
        return Task.FromResult(Console.ReadLine() ?? "");
    }

    private static Task<(string selectedOption, string? modifiedContent)> GetReviewConfirmationAsync(
        string currentContent,
        IReadOnlyList<ConfirmationOption> options,
        CancellationToken cancellationToken
    )
    {
        Console.WriteLine("当前内容：");
        Console.WriteLine("---");
        Console.WriteLine(currentContent);
        Console.WriteLine("---");
        Console.WriteLine();

        // 显示选项
        Console.WriteLine("选项：");
        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            Console.WriteLine($"  {i + 1}. {opt.Label}");
        }
        Console.WriteLine();

        Console.Write("选择操作：");
        var input = Console.ReadLine()?.Trim();

        if (int.TryParse(input, out var index) && index > 0 && index <= options.Count)
        {
            var selectedOpt = options[index - 1];
            string? modifiedContent = null;

            if (selectedOpt.Id == "edit")
            {
                Console.WriteLine("输入修改后的内容（输入空行结束）：");
                var lines = new List<string>();
                string? line;
                while ((line = Console.ReadLine()) != null && !string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
                modifiedContent = string.Join(Environment.NewLine, lines);
            }

            return Task.FromResult((selectedOpt.Id, modifiedContent));
        }

        var defaultOpt = options.FirstOrDefault(o => o.IsDefault);
        return Task.FromResult<(string, string?)>(
            (defaultOpt?.Id ?? (options.Count > 0 ? options[0].Id : "approve"), null)
        );
    }
}
