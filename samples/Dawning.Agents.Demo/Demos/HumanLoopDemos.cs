using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Core.HumanLoop;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Week 10: 人机协作演示
/// </summary>
public static class HumanLoopDemos
{
    /// <summary>
    /// 人机协作演示
    /// </summary>
    public static async Task RunHumanLoopDemo()
    {
        ConsoleHelper.PrintDivider("🤝 人机协作 (Human-in-the-Loop) 演示");

        Console.WriteLine("\n人机协作允许 Agent 在关键决策点请求人工介入：");
        Console.WriteLine("  • ConfirmationRequest: 危险操作确认");
        Console.WriteLine("  • RequestInput: 请求人工输入");
        Console.WriteLine("  • Escalation: 升级到人工处理");
        Console.WriteLine("  • Notification: 通知人类\n");

        // 创建控制台交互处理器
        var handler = new ConsoleInteractionHandler();

        // ====================================================================
        // 1. 确认请求演示
        // ====================================================================
        ConsoleHelper.PrintDivider("1️⃣ 确认请求 (Confirmation)");
        Console.WriteLine("场景：Agent 准备执行危险操作，需要人工确认\n");

        var confirmRequest = new ConfirmationRequest
        {
            Action = "删除文件",
            Description = "Agent 准备删除以下文件:\n  • /tmp/test.log\n  • /tmp/cache.db\n\n这是不可逆操作，是否确认？",
            RiskLevel = RiskLevel.High,
            Timeout = TimeSpan.FromSeconds(30),
        };

        ConsoleHelper.PrintInfo($"📋 {confirmRequest.Action}");
        Console.WriteLine($"   {confirmRequest.Description.Replace("\n", "\n   ")}\n");
        Console.WriteLine($"   风险等级: {confirmRequest.RiskLevel}");

        var confirmResponse = await handler.RequestConfirmationAsync(confirmRequest);

        var isConfirmed = confirmResponse.SelectedOption == "approve" || confirmResponse.SelectedOption == "yes";
        if (isConfirmed)
        {
            ConsoleHelper.PrintSuccess($"✅ 用户确认了操作");
        }
        else
        {
            ConsoleHelper.PrintWarning($"❌ 用户取消了操作");
            if (!string.IsNullOrEmpty(confirmResponse.Reason))
            {
                Console.WriteLine($"   原因: {confirmResponse.Reason}");
            }
        }

        // ====================================================================
        // 2. 请求输入演示
        // ====================================================================
        ConsoleHelper.PrintDivider("2️⃣ 请求输入 (Request Input)");
        Console.WriteLine("场景：Agent 需要额外信息来完成任务\n");

        var input = await handler.RequestInputAsync(
            "请输入目标部署环境 (dev/staging/prod):",
            defaultValue: "staging"
        );

        ConsoleHelper.PrintSuccess($"✅ 用户输入: {input}");

        // ====================================================================
        // 3. 通知演示
        // ====================================================================
        ConsoleHelper.PrintDivider("3️⃣ 通知 (Notification)");
        Console.WriteLine("场景：Agent 向用户发送不同级别的通知\n");

        await handler.NotifyAsync("任务开始执行...", NotificationLevel.Info);
        await Task.Delay(500);

        await handler.NotifyAsync("检测到潜在的性能问题", NotificationLevel.Warning);
        await Task.Delay(500);

        await handler.NotifyAsync("任务执行完成！", NotificationLevel.Success);

        // ====================================================================
        // 4. 升级请求演示
        // ====================================================================
        ConsoleHelper.PrintDivider("4️⃣ 升级到人工 (Escalation)");
        Console.WriteLine("场景：Agent 遇到无法自动处理的情况，升级给人工\n");

        var escalationRequest = new EscalationRequest
        {
            Reason = "检测到异常的交易模式",
            Description = "订单 #12345 的金额超过了自动审批限额 ($10,000)",
            Severity = EscalationSeverity.High,
            Context = new Dictionary<string, object>
            {
                ["orderId"] = "#12345",
                ["amount"] = 15000,
                ["currency"] = "USD",
            },
            AttemptedSolutions = ["自动风控检查", "规则引擎评估"],
        };

        ConsoleHelper.PrintWarning($"⚠️ 升级原因: {escalationRequest.Reason}");
        Console.WriteLine($"   描述: {escalationRequest.Description}");
        Console.WriteLine($"   严重程度: {escalationRequest.Severity}");
        Console.WriteLine("   已尝试的解决方案:");
        foreach (var solution in escalationRequest.AttemptedSolutions)
        {
            Console.WriteLine($"     • {solution}");
        }
        Console.WriteLine();

        var escalationResult = await handler.EscalateAsync(escalationRequest);

        Console.WriteLine($"升级操作: {escalationResult.Action}");
        if (escalationResult.Resolution != null)
        {
            ConsoleHelper.PrintSuccess($"✅ 处理结果: {escalationResult.Resolution}");
        }
        if (escalationResult.ResolvedBy != null)
        {
            Console.WriteLine($"   处理人: {escalationResult.ResolvedBy}");
        }

        // ====================================================================
        // 5. 审批工作流演示
        // ====================================================================
        ConsoleHelper.PrintDivider("5️⃣ 审批配置 (Approval Config)");
        Console.WriteLine("场景：配置审批策略\n");

        var approvalConfig = new ApprovalConfig
        {
            RequireApprovalForLowRisk = false,
            RequireApprovalForMediumRisk = true,
            ApprovalTimeout = TimeSpan.FromMinutes(30),
            DefaultOnTimeout = "reject",
        };

        Console.WriteLine("审批配置:");
        Console.WriteLine($"  低风险操作需要审批: {approvalConfig.RequireApprovalForLowRisk}");
        Console.WriteLine($"  中风险操作需要审批: {approvalConfig.RequireApprovalForMediumRisk}");
        Console.WriteLine($"  审批超时: {approvalConfig.ApprovalTimeout.TotalMinutes} 分钟");
        Console.WriteLine($"  超时默认操作: {approvalConfig.DefaultOnTimeout}");
        Console.WriteLine();

        // 演示 ApprovalResult 的创建
        Console.WriteLine("审批结果示例:");

        var autoApproved = ApprovalResult.AutoApproved("read_file");
        Console.WriteLine($"  • 自动批准: {autoApproved.Action} - IsApproved={autoApproved.IsApproved}, IsAutoApproved={autoApproved.IsAutoApproved}");

        var approved = ApprovalResult.Approved("deploy", "admin");
        Console.WriteLine($"  • 人工批准: {approved.Action} - ApprovedBy={approved.ApprovedBy}");

        var rejected = ApprovalResult.Rejected("delete_all", "操作过于危险", "security-admin");
        Console.WriteLine($"  • 已拒绝: {rejected.Action} - Reason={rejected.RejectionReason}");

        ConsoleHelper.PrintDivider("演示结束");
        Console.WriteLine("\n人机协作功能让 Agent 在关键时刻获得人工支持，");
        Console.WriteLine("确保重要决策的准确性和安全性。\n");
    }
}
