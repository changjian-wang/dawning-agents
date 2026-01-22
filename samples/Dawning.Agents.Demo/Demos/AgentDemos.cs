using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Agent 相关演示
/// </summary>
public static class AgentDemos
{
    /// <summary>
    /// Agent 演示（ReAct 模式）
    /// </summary>
    public static async Task RunAgentDemo(IAgent agent)
    {
        ConsoleHelper.PrintSection("2. Agent 演示（ReAct 模式）");
        Console.WriteLine($"✓ Agent: {agent.Name}\n");

        var question =
            "帮我搜索 AI Agent 的常见架构模式，然后计算如果一个 Agent 系统有 3 个专家 Agent，每个专家有 4 个工具，总共需要多少个工具调用能力？最后总结多 Agent 协作的优势。";
        Console.WriteLine($"📝 问题：{question}\n");

        var response = await agent.RunAsync(question);

        // 执行过程
        ConsoleHelper.PrintDivider("🔄 执行过程");

        foreach (var step in response.Steps)
        {
            Console.WriteLine($"\n【步骤 {step.StepNumber}】");

            if (!string.IsNullOrEmpty(step.Thought))
            {
                ConsoleHelper.PrintColored($"  💭 思考：{step.Thought.Trim()}", ConsoleColor.Cyan);
            }

            if (!string.IsNullOrEmpty(step.Action))
            {
                ConsoleHelper.PrintColored($"  🎯 动作：{step.Action}", ConsoleColor.Yellow);
                ConsoleHelper.PrintColored($"  📥 输入：{step.ActionInput}", ConsoleColor.Yellow);
            }

            if (!string.IsNullOrEmpty(step.Observation))
            {
                ConsoleHelper.PrintColored(
                    $"  👁️ 观察：{step.Observation.Trim()}",
                    ConsoleColor.Green
                );
            }
        }

        // 执行摘要
        ConsoleHelper.PrintDivider("📊 执行摘要");
        Console.WriteLine($"  状态：{(response.Success ? "✅ 成功" : "❌ 失败")}");
        Console.WriteLine($"  步骤：{response.Steps.Count} 步");
        Console.WriteLine($"  耗时：{response.Duration.TotalMilliseconds:F0}ms");

        if (response.Success && !string.IsNullOrEmpty(response.FinalAnswer))
        {
            ConsoleHelper.PrintColored(
                $"\n  📝 总结：{response.FinalAnswer}",
                ConsoleColor.Magenta
            );
        }

        if (!response.Success && !string.IsNullOrEmpty(response.Error))
        {
            ConsoleHelper.PrintError($"  错误：{response.Error}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Agent + Memory 多轮对话演示
    /// </summary>
    public static async Task RunAgentMemoryDemo(IAgent agent, IConversationMemory memory)
    {
        ConsoleHelper.PrintSection("Agent + Memory 多轮对话演示");
        Console.WriteLine($"✓ Agent: {agent.Name}");
        Console.WriteLine($"✓ Memory 类型: {memory.GetType().Name}");
        Console.WriteLine("\n演示 Agent 如何在多轮对话中自动保存记忆...\n");

        // 预设的多轮对话问题
        var questions = new[] { "计算 15 + 27 等于多少？", "再把刚才的结果乘以 2", "今天是几号？" };

        foreach (var question in questions)
        {
            ConsoleHelper.PrintDivider($"📝 问题：{question}");

            var response = await agent.RunAsync(question);

            // 显示执行步骤
            foreach (var step in response.Steps)
            {
                if (!string.IsNullOrEmpty(step.Action))
                {
                    ConsoleHelper.PrintColored(
                        $"  🎯 {step.Action}({step.ActionInput})",
                        ConsoleColor.Yellow
                    );
                    ConsoleHelper.PrintColored(
                        $"  👁️ {step.Observation?.Trim()}",
                        ConsoleColor.Green
                    );
                }
            }

            if (response.Success && !string.IsNullOrEmpty(response.FinalAnswer))
            {
                ConsoleHelper.PrintColored(
                    $"\n  💬 回答：{response.FinalAnswer}\n",
                    ConsoleColor.Cyan
                );
            }

            // 显示 Memory 状态
            var messages = await memory.GetMessagesAsync();
            ConsoleHelper.PrintDim($"  📚 Memory 状态: {messages.Count} 条消息");

            // 显示最近的消息摘要
            var recent = messages.TakeLast(4).ToList();
            foreach (var msg in recent)
            {
                var role = msg.Role == "user" ? "👤" : "🤖";
                var content = msg.Content.Length > 50 ? msg.Content[..50] + "..." : msg.Content;
                ConsoleHelper.PrintDim($"     {role} {content}");
            }

            Console.WriteLine();
        }

        // 最终统计
        ConsoleHelper.PrintDivider("📊 Memory 统计");
        var allMessages = await memory.GetMessagesAsync();
        Console.WriteLine($"  总消息数: {allMessages.Count}");
        Console.WriteLine($"  用户消息: {allMessages.Count(m => m.Role == "user")}");
        Console.WriteLine($"  助手消息: {allMessages.Count(m => m.Role == "assistant")}");
        var totalTokens = await memory.GetTokenCountAsync();
        Console.WriteLine($"  估计 Token: {totalTokens}");
    }
}
