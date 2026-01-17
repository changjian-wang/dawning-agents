using DawningAgents.Core.LLM;

Console.WriteLine("=== DawningAgents LLM 演示 (Ollama 本地模型) ===\n");

// 使用本地 Ollama 模型
var model = args.Length > 0 ? args[0] : "deepseek-coder:6.7b";
Console.WriteLine($"使用模型: {model}\n");

ILLMProvider provider;
try
{
    provider = new OllamaProvider(model);
}
catch (Exception ex)
{
    Console.WriteLine($"创建提供者失败: {ex.Message}");
    Console.WriteLine("请确保 Ollama 正在运行: ollama serve");
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
    Console.WriteLine("请确保 Ollama 正在运行，且已下载模型。");
    Console.WriteLine($"运行: ollama pull {model}");
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
