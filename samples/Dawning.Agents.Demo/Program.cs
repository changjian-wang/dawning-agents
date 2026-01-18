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
Console.WriteLine($"问题：{complexQuestion}\n");
var agentResponse = await agent.RunAsync(complexQuestion);

Console.WriteLine($"执行结果: {(agentResponse.Success ? "成功" : "失败")}");
Console.WriteLine($"执行步骤: {agentResponse.Steps.Count}");
Console.WriteLine($"执行时间: {agentResponse.Duration.TotalMilliseconds:F0}ms");

if (agentResponse.Success)
{
    Console.WriteLine($"最终答案: {agentResponse.FinalAnswer}");
}
else
{
    Console.WriteLine($"错误: {agentResponse.Error}");
}

// 显示执行步骤详情
Console.WriteLine("\n执行步骤详情：");
foreach (var step in agentResponse.Steps)
{
    Console.WriteLine($"  步骤 {step.StepNumber}:");

    // 显示原始输出（截取前 200 字符）
    if (!string.IsNullOrEmpty(step.RawOutput))
    {
        var preview = step.RawOutput.Length > 200 ? step.RawOutput[..200] + "..." : step.RawOutput;
        Console.WriteLine($"    原始输出: {preview.Replace("\n", " ")}");
    }

    if (!string.IsNullOrEmpty(step.Thought))
    {
        Console.WriteLine($"    Thought: {step.Thought[..Math.Min(100, step.Thought.Length)]}...");
    }

    if (!string.IsNullOrEmpty(step.Action))
    {
        Console.WriteLine($"    Action: {step.Action}");
        Console.WriteLine($"    Input: {step.ActionInput}");
    }

    if (!string.IsNullOrEmpty(step.Observation))
    {
        Console.WriteLine(
            $"    Observation: {step.Observation[..Math.Min(80, step.Observation.Length)]}..."
        );
    }
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
