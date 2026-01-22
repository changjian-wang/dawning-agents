using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Memory 系统演示
/// </summary>
public static class MemoryDemos
{
    /// <summary>
    /// Memory 系统演示（滑动窗口）
    /// </summary>
    public static async Task RunMemoryDemo(
        ILLMProvider provider,
        IConversationMemory memory,
        ITokenCounter tokenCounter
    )
    {
        ConsoleHelper.PrintSection("5. Memory 系统演示（滑动窗口）");

        var windowMemory = memory as WindowMemory;
        if (windowMemory != null)
        {
            Console.WriteLine($"✓ 使用 WindowMemory，窗口大小: {windowMemory.WindowSize}");
        }
        else
        {
            Console.WriteLine($"✓ 使用 {memory.GetType().Name}");
        }

        Console.WriteLine($"✓ Token 计数器: {tokenCounter.ModelName}");
        Console.WriteLine("\n输入 'quit' 退出，输入 'status' 查看记忆状态\n");

        var systemPrompt = "你是 Dawn，一个简洁的 AI 助手。回答要简短，不超过 50 字。";

        while (true)
        {
            Console.Write("你：");
            var input = Console.ReadLine();

            if (
                string.IsNullOrWhiteSpace(input)
                || input.Equals("quit", StringComparison.OrdinalIgnoreCase)
                || input.Equals("exit", StringComparison.OrdinalIgnoreCase)
            )
            {
                break;
            }

            // 查看记忆状态
            if (input.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                await PrintMemoryStatus(memory);
                continue;
            }

            // 添加用户消息到记忆
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = input }
            );

            // 获取上下文并调用 LLM
            var context = await memory.GetContextAsync();
            var messagesForLlm = context.ToList();

            Console.Write("Dawn：");
            var fullResponse = new System.Text.StringBuilder();

            await foreach (
                var chunk in provider.ChatStreamAsync(
                    messagesForLlm,
                    new ChatCompletionOptions { SystemPrompt = systemPrompt, MaxTokens = 200 }
                )
            )
            {
                Console.Write(chunk);
                fullResponse.Append(chunk);
            }

            Console.WriteLine();

            // 添加助手回复到记忆
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "assistant", Content = fullResponse.ToString() }
            );

            // 显示记忆统计
            var tokenCount = await memory.GetTokenCountAsync();
            ConsoleHelper.PrintDim($"  [消息数: {memory.MessageCount}, Token: ~{tokenCount}]");
            Console.WriteLine();
        }

        // 退出前显示最终状态
        Console.WriteLine("\n📊 最终记忆状态：");
        await PrintMemoryStatus(memory);
    }

    /// <summary>
    /// 打印记忆状态
    /// </summary>
    public static async Task PrintMemoryStatus(IConversationMemory memory)
    {
        var messages = await memory.GetMessagesAsync();
        var tokenCount = await memory.GetTokenCountAsync();

        ConsoleHelper.PrintDivider("📝 记忆状态");
        Console.WriteLine($"  消息数量: {memory.MessageCount}");
        Console.WriteLine($"  Token 估算: ~{tokenCount}");
        Console.WriteLine();

        if (messages.Count > 0)
        {
            Console.WriteLine("  最近消息:");
            foreach (var msg in messages.TakeLast(6))
            {
                var preview = msg.Content.Length > 40 ? msg.Content[..40] + "..." : msg.Content;
                var role = msg.Role == "user" ? "👤" : "🤖";
                ConsoleHelper.PrintDim($"    {role} {preview.Replace("\n", " ")}");
            }
        }

        Console.WriteLine();
    }
}
