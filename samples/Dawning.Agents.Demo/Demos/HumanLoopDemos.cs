using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Human-in-the-Loop 演示
/// </summary>
public static class HumanLoopDemos
{
    /// <summary>
    /// 运行 Human-in-the-Loop 演示
    /// </summary>
    public static async Task RunHumanLoopDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("Human-in-the-Loop 演示");
        Console.WriteLine("演示人工审批、交互式确认、升级处理等功能\n");

        // 1. 确认请求模型演示
        await RunConfirmationModelDemo();

        // 2. 风险级别演示
        await RunRiskLevelDemo();

        // 3. 审批流程说明
        PrintApprovalWorkflow();

        ConsoleHelper.PrintSuccess("\nHuman-in-the-Loop 演示完成！");
    }

    private static async Task RunConfirmationModelDemo()
    {
        ConsoleHelper.PrintDivider("1. 确认请求模型 (ConfirmationRequest)");

        Console.WriteLine("  Human-in-the-Loop 系统使用结构化的确认请求:\n");

        // 模拟不同类型的确认请求
        var requests = new[]
        {
            new { Type = "Binary", Action = "DeleteFile", Desc = "删除 /tmp/test.txt", Risk = "High" },
            new { Type = "MultiChoice", Action = "SelectModel", Desc = "选择 LLM 模型", Risk = "Low" },
            new { Type = "FreeformInput", Action = "ProvideReason", Desc = "输入拒绝原因", Risk = "Medium" },
            new { Type = "Review", Action = "ReviewCode", Desc = "审核生成的代码", Risk = "Medium" },
        };

        foreach (var req in requests)
        {
            Console.WriteLine($"  📋 {req.Type} 类型:");
            Console.WriteLine($"     操作: {req.Action}");
            Console.WriteLine($"     描述: {req.Desc}");
            Console.WriteLine($"     风险: {req.Risk}");
            Console.WriteLine();
        }

        await Task.CompletedTask;
    }

    private static async Task RunRiskLevelDemo()
    {
        ConsoleHelper.PrintDivider("2. 风险级别策略");

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
            Console.WriteLine($"  {risk.Icon} {risk.Level,-10} {risk.Policy}");
        }

        Console.WriteLine("\n  示例场景:");
        Console.WriteLine("    Low:      读取配置文件");
        Console.WriteLine("    Medium:   修改用户设置");
        Console.WriteLine("    High:     删除用户数据");
        Console.WriteLine("    Critical: 部署到生产环境");

        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static void PrintApprovalWorkflow()
    {
        ConsoleHelper.PrintDivider("3. 审批工作流说明");

        Console.WriteLine(
            """
              HumanInLoopAgent 工作流程:

              1. Agent 执行请求
                 │
                 ▼
              2. 检查操作风险级别
                 │
                 ├─ Low → 自动批准 (如果启用)
                 │
                 └─ Medium/High/Critical
                    │
                    ▼
              3. 创建 ConfirmationRequest
                 │
                 ▼
              4. 调用 IHumanInteractionHandler
                 │
                 ├─ ConsoleInteractionHandler: 命令行交互
                 ├─ AsyncCallbackHandler: 异步回调
                 └─ 自定义实现: Web API, Slack, Email 等
                    │
                    ▼
              5. 等待人工响应 (带超时)
                 │
                 ├─ 批准 → 继续执行
                 ├─ 拒绝 → 返回拒绝结果
                 └─ 超时 → 根据配置处理

              关键接口:
              - IHumanInteractionHandler: 人机交互处理器
              - ApprovalWorkflow: 审批流程管理
              - HumanInLoopAgent: 包装 Agent 添加人工干预

            """
        );
    }
}
