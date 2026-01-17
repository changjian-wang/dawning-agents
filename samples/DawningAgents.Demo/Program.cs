using DawningAgents.Abstractions.LLM;
using DawningAgents.Core.LLM;

Console.WriteLine("=== DawningAgents LLM 演示 ===\n");

// 使用工厂从环境变量自动选择 Provider
// 支持: AZURE_OPENAI_*, OPENAI_API_KEY, 或默认 Ollama
var config = LLMConfiguration.FromEnvironment();

// 允许命令行参数覆盖模型
if (args.Length > 0)
{
    config = config with { Model = args[0] };
}

Console.WriteLine($"提供者: {config.ProviderType}");
Console.WriteLine($"模型: {config.Model}");
if (!string.IsNullOrEmpty(config.Endpoint))
{
    Console.WriteLine($"端点: {config.Endpoint}");
}
Console.WriteLine();

ILLMProvider provider;
try
{
    provider = LLMProviderFactory.Create(config);
    Console.WriteLine($"✓ 已创建 {provider.Name} 提供者\n");
}
catch (Exception ex)
{
    Console.WriteLine($"创建提供者失败: {ex.Message}");
    if (config.ProviderType == LLMProviderType.Ollama)
    {
        Console.WriteLine("请确保 Ollama 正在运行: ollama serve");
        Console.WriteLine($"并下载模型: ollama pull {config.Model}");
    }
    return;
}

// 1. 简单聊天
Console.WriteLine("1. 简单聊天：");
Console.WriteLine("问题：用两句话解释什么是 AI Agent？\n");

try
{
    var response = await provider.ChatAsync(
        [new ChatMessage("user", "用两句话解释什么是 AI Agent？")],
        new ChatCompletionOptions { MaxTokens = 200 });

    Console.WriteLine($"回复：{response.Content}");
    Console.WriteLine($"Token 数：输入={response.PromptTokens}, 输出={response.CompletionTokens}, 总计={response.TotalTokens}\n");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"请求失败: {ex.Message}");
    if (config.ProviderType == LLMProviderType.Ollama)
    {
        Console.WriteLine("请确保 Ollama 正在运行，且已下载模型。");
        Console.WriteLine($"运行: ollama pull {config.Model}");
    }
    else
    {
        Console.WriteLine("请检查 API Key 和网络连接。");
    }
    return;
}

// 2. 流式聊天
Console.WriteLine("2. 流式聊天：");
Console.WriteLine("问题：慢慢从 1 数到 5。\n");
Console.Write("回复：");

await foreach (var chunk in provider.ChatStreamAsync(
    [new ChatMessage("user", "慢慢从 1 数到 5，每个数字后面加个句号。")],
    new ChatCompletionOptions { MaxTokens = 100 }))
{
    Console.Write(chunk);
}

Console.WriteLine("\n");

// 3. 交互式对话
Console.WriteLine("3. 交互式对话（输入 'quit' 退出）：");

var messages = new List<ChatMessage>();
var systemPrompt = "你是一个名叫 Dawn 的 AI 编程助手。回答要简洁。";

while (true)
{
    Console.Write("\n你：");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    messages.Add(new ChatMessage("user", input));

    Console.Write("Dawn：");

    var fullResponse = new System.Text.StringBuilder();
    await foreach (var chunk in provider.ChatStreamAsync(
        messages,
        new ChatCompletionOptions { SystemPrompt = systemPrompt, MaxTokens = 500 }))
    {
        Console.Write(chunk);
        fullResponse.Append(chunk);
    }
    Console.WriteLine();

    messages.Add(new ChatMessage("assistant", fullResponse.ToString()));
}

Console.WriteLine("\n再见！");
