using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("=== Dawning.Agents 演示 ===\n");

// 使用 Host 构建（自动配置 Configuration、Logging）
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLLMProvider(builder.Configuration);
builder.Services.AddReActAgent(options =>
{
    options.Name = "DawnAgent";
    options.Instructions = "你是一个有用的 AI 助手，善于分析问题并给出答案。";
    options.MaxSteps = 5;
});

using var host = builder.Build();

// 从 DI 获取 Provider
ILLMProvider provider;
try
{
    provider = host.Services.GetRequiredService<ILLMProvider>();
    Console.WriteLine($"✓ 已创建 {provider.Name} 提供者\n");
}
catch (Exception ex)
{
    Console.WriteLine($"创建提供者失败: {ex.Message}");
    Console.WriteLine("请检查 appsettings.json 配置或环境变量");
    return;
}

// 1. 简单聊天
Console.WriteLine("1. 简单聊天：");
Console.WriteLine("问题：用两句话解释什么是 AI Agent？\n");

try
{
    var response = await provider.ChatAsync(
        [new ChatMessage("user", "用两句话解释什么是 AI Agent？")],
        new ChatCompletionOptions { MaxTokens = 200 }
    );

    Console.WriteLine($"回复：{response.Content}");
    Console.WriteLine(
        $"Token 数：输入={response.PromptTokens}, 输出={response.CompletionTokens}, 总计={response.TotalTokens}\n"
    );
}
catch (Exception ex)
{
    Console.WriteLine($"请求失败: {ex.Message}");
    Console.WriteLine("请确保服务正在运行，且配置正确。");
    return;
}

// 2. Agent 演示
Console.WriteLine("2. Agent 演示（ReAct 模式）：");
var agent = host.Services.GetRequiredService<IAgent>();
Console.WriteLine($"✓ 已创建 Agent: {agent.Name}\n");

// 使用需要多步推理的复杂问题
var complexQuestion = "帮我计算 23 * 17 的结果，然后查询北京的天气，最后总结一下。";
Console.WriteLine($"📝 问题：{complexQuestion}\n");
var agentResponse = await agent.RunAsync(complexQuestion);

// 显示执行步骤详情（使用清晰的格式）
Console.WriteLine("─────────────────────────────────────────────────────────────");
Console.WriteLine("                        🔄 执行过程");
Console.WriteLine("─────────────────────────────────────────────────────────────");

foreach (var step in agentResponse.Steps)
{
    Console.WriteLine($"\n【步骤 {step.StepNumber}】");

    if (!string.IsNullOrEmpty(step.Thought))
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  💭 思考：{step.Thought.Trim()}");
        Console.ResetColor();
    }

    if (!string.IsNullOrEmpty(step.Action))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  🎯 动作：{step.Action}");
        Console.WriteLine($"  📥 输入：{step.ActionInput}");
        Console.ResetColor();
    }

    if (!string.IsNullOrEmpty(step.Observation))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  👁️ 观察：{step.Observation.Trim()}");
        Console.ResetColor();
    }
}

Console.WriteLine("\n─────────────────────────────────────────────────────────────");
Console.WriteLine("                        📊 执行摘要");
Console.WriteLine("─────────────────────────────────────────────────────────────");
Console.WriteLine($"  状态：{(agentResponse.Success ? "✅ 成功" : "❌ 失败")}");
Console.WriteLine($"  步骤：{agentResponse.Steps.Count} 步");
Console.WriteLine($"  耗时：{agentResponse.Duration.TotalMilliseconds:F0}ms");
if (!agentResponse.Success && !string.IsNullOrEmpty(agentResponse.Error))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  错误：{agentResponse.Error}");
    Console.ResetColor();
}

Console.WriteLine();

// 3. 流式聊天
Console.WriteLine("3. 流式聊天：");
Console.WriteLine("问题：慢慢从 1 数到 5。\n");
Console.Write("回复：");

await foreach (
    var chunk in provider.ChatStreamAsync(
        [new ChatMessage("user", "慢慢从 1 数到 5，每个数字后面加个句号。")],
        new ChatCompletionOptions { MaxTokens = 100 }
    )
)
{
    Console.Write(chunk);
}

Console.WriteLine("\n");

// 4. 交互式对话
Console.WriteLine("4. 交互式对话（输入 'quit' 退出）：");

var messages = new List<ChatMessage>();
var systemPrompt = "你是一个名叫 Dawn 的 AI 编程助手。回答要简洁。";

while (true)
{
    Console.Write("\n你：");
    var input = Console.ReadLine();

    if (
        string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase)
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
    Console.WriteLine();

    messages.Add(new ChatMessage("assistant", fullResponse.ToString()));
}

Console.WriteLine("\n再见！");
