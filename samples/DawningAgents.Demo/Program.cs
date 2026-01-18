using DawningAgents.Abstractions.LLM;
using DawningAgents.Core.LLM;
using Microsoft.Extensions.Configuration;

Console.WriteLine("=== DawningAgents LLM 演示 ===\n");

// 构建配置：支持多种配置源
// 优先级（从低到高）：appsettings.json < appsettings.{Environment}.json < 环境变量 < 命令行参数
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true,
        reloadOnChange: true
    )
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// 从配置绑定 LLMOptions
var options = new LLMOptions();
configuration.GetSection(LLMOptions.SectionName).Bind(options);

// 如果 appsettings.json 中没有配置，则尝试从传统环境变量读取
if (options.ProviderType == LLMProviderType.Ollama && string.IsNullOrEmpty(options.ApiKey))
{
    var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
    var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey))
    {
        options.ProviderType = LLMProviderType.AzureOpenAI;
        options.Endpoint = azureEndpoint;
        options.ApiKey = azureApiKey;
        options.Model = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";
    }
    else if (!string.IsNullOrEmpty(openaiApiKey))
    {
        options.ProviderType = LLMProviderType.OpenAI;
        options.ApiKey = openaiApiKey;
        options.Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o";
    }
    else
    {
        // 默认使用 Ollama
        options.Endpoint ??= "http://localhost:11434";
        options.Model = string.IsNullOrEmpty(options.Model) ? "deepseek-coder:1.3b" : options.Model;
    }
}

Console.WriteLine($"提供者: {options.ProviderType}");
Console.WriteLine($"模型: {options.Model}");
if (!string.IsNullOrEmpty(options.Endpoint))
{
    Console.WriteLine($"端点: {options.Endpoint}");
}
Console.WriteLine();

ILLMProvider provider;
try
{
    provider = LLMProviderFactory.Create(options);
    Console.WriteLine($"✓ 已创建 {provider.Name} 提供者\n");
}
catch (Exception ex)
{
    Console.WriteLine($"创建提供者失败: {ex.Message}");
    if (options.ProviderType == LLMProviderType.Ollama)
    {
        Console.WriteLine("请确保 Ollama 正在运行: ollama serve");
        Console.WriteLine($"并下载模型: ollama pull {options.Model}");
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
        new ChatCompletionOptions { MaxTokens = 200 }
    );

    Console.WriteLine($"回复：{response.Content}");
    Console.WriteLine(
        $"Token 数：输入={response.PromptTokens}, 输出={response.CompletionTokens}, 总计={response.TotalTokens}\n"
    );
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"请求失败: {ex.Message}");
    if (options.ProviderType == LLMProviderType.Ollama)
    {
        Console.WriteLine("请确保 Ollama 正在运行，且已下载模型。");
        Console.WriteLine($"运行: ollama pull {options.Model}");
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

// 3. 交互式对话
Console.WriteLine("3. 交互式对话（输入 'quit' 退出）：");

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
