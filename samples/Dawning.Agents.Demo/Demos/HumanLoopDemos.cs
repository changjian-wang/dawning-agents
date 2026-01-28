using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Demo.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Human-in-the-Loop 演示
/// </summary>
public static class HumanLoopDemos
{
    /// <summary>
    /// 运行 Human-in-the-Loop 演示
    /// </summary>
    public static async Task RunHumanLoopDemo(IServiceProvider services)
    {
        ConsoleHelper.PrintSection("Human-in-the-Loop 演示");
        Console.WriteLine("演示人工审批、交互式确认、升级处理等功能\n");

        var handler = services.GetRequiredService<IHumanInteractionHandler>();

        // 1. Binary 确认演示
        await RunBinaryConfirmationDemo(handler);

        // 2. MultiChoice 确认演示
        await RunMultiChoiceDemo(handler);

        // 3. FreeformInput 演示
        await RunFreeformInputDemo(handler);

        // 4. Review 确认演示
        await RunReviewDemo(handler);

        // 5. 风险级别说明
        PrintRiskLevelInfo();

        ConsoleHelper.PrintSuccess("\nHuman-in-the-Loop 演示完成！");
    }

    private static async Task RunBinaryConfirmationDemo(IHumanInteractionHandler handler)
    {
        ConsoleHelper.PrintDivider("1. Binary 确认 (是/否)");

        var request = new ConfirmationRequest
        {
            Action = "DeleteFile",
            Description = "确认删除文件 /tmp/test.txt？此操作不可恢复。",
            Type = ConfirmationType.Binary,
            RiskLevel = RiskLevel.High,
            Context = new Dictionary<string, object>
            {
                ["文件路径"] = "/tmp/test.txt",
                ["文件大小"] = "1.2 MB",
                ["最后修改"] = "2026-01-26 10:30:00",
            },
        };

        Console.WriteLine("  发送 Binary 类型确认请求...\n");

        var response = await handler.RequestConfirmationAsync(request);

        Console.WriteLine();
        var isApproved =
            response.SelectedOption.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || response.SelectedOption.Equals("approve", StringComparison.OrdinalIgnoreCase)
            || response.SelectedOption.Equals("y", StringComparison.OrdinalIgnoreCase);

        if (isApproved)
        {
            ConsoleHelper.PrintSuccess("  ✅ 用户批准，可以执行删除操作");
        }
        else
        {
            ConsoleHelper.PrintWarning($"  ❌ 用户拒绝: {response.Reason ?? "无理由"}");
        }

        Console.WriteLine();
    }

    private static async Task RunMultiChoiceDemo(IHumanInteractionHandler handler)
    {
        ConsoleHelper.PrintDivider("2. MultiChoice 确认 (多选一)");

        var request = new ConfirmationRequest
        {
            Action = "SelectModel",
            Description = "请选择要使用的 LLM 模型：",
            Type = ConfirmationType.MultiChoice,
            RiskLevel = RiskLevel.Low,
            Options =
            [
                new ConfirmationOption
                {
                    Id = "fast",
                    Label = "qwen2.5:0.5b (快速)",
                    IsDefault = true,
                },
                new ConfirmationOption { Id = "balanced", Label = "qwen2.5:7b (平衡)" },
                new ConfirmationOption { Id = "quality", Label = "qwen2.5:72b (高质量)" },
            ],
        };

        Console.WriteLine("  发送 MultiChoice 类型确认请求...\n");

        var response = await handler.RequestConfirmationAsync(request);

        Console.WriteLine();
        if (!string.IsNullOrWhiteSpace(response.SelectedOption))
        {
            ConsoleHelper.PrintSuccess($"  ✅ 用户选择: {response.SelectedOption}");
        }
        else
        {
            ConsoleHelper.PrintWarning("  ❌ 用户取消选择");
        }

        Console.WriteLine();
    }

    private static async Task RunFreeformInputDemo(IHumanInteractionHandler handler)
    {
        ConsoleHelper.PrintDivider("3. FreeformInput 确认 (自由输入)");

        var request = new ConfirmationRequest
        {
            Action = "ProvideReason",
            Description = "请输入拒绝此操作的原因：",
            Type = ConfirmationType.FreeformInput,
            RiskLevel = RiskLevel.Medium,
        };

        Console.WriteLine("  发送 FreeformInput 类型确认请求...\n");

        var response = await handler.RequestConfirmationAsync(request);

        Console.WriteLine();
        if (!string.IsNullOrWhiteSpace(response.FreeformInput))
        {
            ConsoleHelper.PrintSuccess($"  📝 用户输入: {response.FreeformInput}");
        }
        else
        {
            ConsoleHelper.PrintWarning("  ❌ 用户未提供输入");
        }

        Console.WriteLine();
    }

    private static async Task RunReviewDemo(IHumanInteractionHandler handler)
    {
        ConsoleHelper.PrintDivider("4. Review 确认 (审核内容)");

        var codeToReview = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
                public int Subtract(int a, int b) => a - b;
            }
            """;

        var request = new ConfirmationRequest
        {
            Action = "ReviewCode",
            Description = codeToReview,
            Type = ConfirmationType.Review,
            RiskLevel = RiskLevel.Medium,
            Options =
            [
                new ConfirmationOption { Id = "approve", Label = "批准" },
                new ConfirmationOption { Id = "modify", Label = "修改后批准" },
                new ConfirmationOption
                {
                    Id = "reject",
                    Label = "拒绝",
                    IsDangerous = true,
                },
            ],
        };

        Console.WriteLine("  发送 Review 类型确认请求（审核代码）...\n");

        var response = await handler.RequestConfirmationAsync(request);

        Console.WriteLine();
        var isApproved =
            response.SelectedOption.Equals("approve", StringComparison.OrdinalIgnoreCase)
            || response.SelectedOption.Equals("modify", StringComparison.OrdinalIgnoreCase);

        if (isApproved)
        {
            if (!string.IsNullOrWhiteSpace(response.ModifiedContent))
            {
                ConsoleHelper.PrintSuccess("  ✅ 用户批准（有修改）:");
                Console.WriteLine($"  {response.ModifiedContent}");
            }
            else
            {
                ConsoleHelper.PrintSuccess("  ✅ 用户批准原内容");
            }
        }
        else
        {
            ConsoleHelper.PrintWarning($"  ❌ 用户拒绝: {response.Reason ?? "无理由"}");
        }

        Console.WriteLine();
    }

    private static void PrintRiskLevelInfo()
    {
        ConsoleHelper.PrintDivider("5. 风险级别说明");

        Console.WriteLine("  不同风险级别的处理策略:\n");

        var riskLevels = new[]
        {
            (Level: "Low", Icon: "🟢", Policy: "可自动批准，无需人工干预"),
            (Level: "Medium", Icon: "🟡", Policy: "建议人工确认，可配置自动超时批准"),
            (Level: "High", Icon: "🟠", Policy: "必须人工确认，超时默认拒绝"),
            (Level: "Critical", Icon: "🔴", Policy: "必须多人审批，不允许超时批准"),
        };

        foreach (var risk in riskLevels)
        {
            Console.WriteLine($"  {risk.Icon} {risk.Level, -10} {risk.Policy}");
        }

        Console.WriteLine("\n  示例场景:");
        Console.WriteLine("    Low:      读取配置文件、查询数据");
        Console.WriteLine("    Medium:   修改用户设置、发送通知");
        Console.WriteLine("    High:     删除用户数据、执行系统命令");
        Console.WriteLine("    Critical: 部署到生产环境、修改权限");
    }
}
