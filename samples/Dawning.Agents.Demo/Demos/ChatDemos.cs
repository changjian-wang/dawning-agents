using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// 聊天相关演示
/// </summary>
public static class ChatDemos
{
    /// <summary>
    /// 简单聊天演示
    /// </summary>
    public static async Task RunChatDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("1. 简单聊天");
        Console.WriteLine("问题：什么是 ReAct 模式？它如何帮助 AI Agent 解决复杂问题？\n");

        try
        {
            var response = await provider.ChatAsync(
                [
                    new ChatMessage(
                        "user",
                        "什么是 ReAct 模式？它如何帮助 AI Agent 解决复杂问题？用简洁的话解释。"
                    ),
                ],
                new ChatCompletionOptions { MaxTokens = 300 }
            );

            Console.WriteLine($"回复：{response.Content}");
            ConsoleHelper.PrintDim(
                $"Token: 输入={response.PromptTokens}, 输出={response.CompletionTokens}, 总计={response.TotalTokens}"
            );
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError($"请求失败: {ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// 流式聊天演示
    /// </summary>
    public static async Task RunStreamDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("3. 流式聊天");
        Console.WriteLine("问题：AI Agent 常用的工具类型有哪些？\n");
        Console.Write("回复：");

        await foreach (
            var chunk in provider.ChatStreamAsync(
                [
                    new ChatMessage(
                        "user",
                        "列举 AI Agent 常用的 5 种工具类型，每种用一句话说明用途。"
                    ),
                ],
                new ChatCompletionOptions { MaxTokens = 400 }
            )
        )
        {
            Console.Write(chunk);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// 交互式对话演示
    /// </summary>
    public static async Task RunInteractiveChat(ILLMProvider provider)
    {
        ConsoleHelper.PrintSection("4. 交互式对话");
        Console.WriteLine("输入 'quit' 或 'exit' 退出\n");

        var messages = new List<ChatMessage>();
        var systemPrompt =
            "你是一个名叫 Dawn 的 AI Agent 专家，精通 Agent 架构设计、工具调用和多 Agent 协作。回答要简洁。";

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

            messages.Add(new ChatMessage("user", input));

            Console.Write("Dawn：");
            var fullResponse = new System.Text.StringBuilder();

            await foreach (
                var chunk in provider.ChatStreamAsync(
                    messages,
                    new ChatCompletionOptions { SystemPrompt = systemPrompt, MaxTokens = 500 }
                )
            )
            {
                Console.Write(chunk);
                fullResponse.Append(chunk);
            }

            Console.WriteLine("\n");
            messages.Add(new ChatMessage("assistant", fullResponse.ToString()));
        }
    }
}
