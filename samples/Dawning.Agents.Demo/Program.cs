using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using Dawning.Agents.Demo.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 解析命令行参数
var (showHelp, runMode) = ParseArgs(args);

if (showHelp)
{
    ShowHelp();
    return;
}

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║             Dawning.Agents 演示                           ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

// 构建 Host
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddLLMProvider(builder.Configuration);

// 注册内置工具 + 自定义工具
builder.Services.AddBuiltInTools();
builder.Services.AddToolsFrom<DemoTools>();

builder.Services.AddReActAgent(options =>
{
    options.Name = "DawnAgent";
    options.Instructions = "你是一个专业的 AI Agent 专家，擅长分析问题并使用工具解决问题。";
    options.MaxSteps = 5;
});

using var host = builder.Build();

// 确保工具已注册
host.Services.EnsureToolsRegistered();

// 获取服务
var provider = GetProvider(host.Services);
if (provider == null)
{
    return;
}

var agent = host.Services.GetRequiredService<IAgent>();

// 根据模式运行
switch (runMode)
{
    case RunMode.Chat:
        await RunChatDemo(provider);
        break;
    case RunMode.Agent:
        await RunAgentDemo(agent);
        break;
    case RunMode.Stream:
        await RunStreamDemo(provider);
        break;
    case RunMode.Interactive:
        await RunInteractiveChat(provider);
        break;
    default: // All
        await RunChatDemo(provider);
        await RunAgentDemo(agent);
        await RunStreamDemo(provider);
        await RunInteractiveChat(provider);
        break;
}

Console.WriteLine("\n再见！");

// ============================================================================
// 辅助方法
// ============================================================================

static (bool showHelp, RunMode mode) ParseArgs(string[] args)
{
    var showHelp = args.Contains("--help") || args.Contains("-h");
    var mode = RunMode.All;

    if (args.Contains("--chat"))
    {
        mode = RunMode.Chat;
    }
    else if (args.Contains("--agent"))
    {
        mode = RunMode.Agent;
    }
    else if (args.Contains("--stream"))
    {
        mode = RunMode.Stream;
    }
    else if (args.Contains("--interactive") || args.Contains("-i"))
    {
        mode = RunMode.Interactive;
    }

    return (showHelp, mode);
}

static void ShowHelp()
{
    Console.WriteLine(
        """
        Dawning.Agents Demo

        用法: dotnet run [选项]

        运行模式:
          --chat          只运行简单聊天演示
          --agent         只运行 Agent 演示
          --stream        只运行流式聊天演示
          -i, --interactive  只运行交互式对话
          -h, --help      显示帮助信息

        配置提供者 (编辑 appsettings.json):
          LLM.ProviderType = "Ollama"      本地 Ollama (默认)
          LLM.ProviderType = "OpenAI"      OpenAI API
          LLM.ProviderType = "AzureOpenAI" Azure OpenAI

        环境变量快速切换:
          $env:LLM__ProviderType = "Ollama"
          $env:LLM__Model = "qwen2.5:7b"
          $env:LLM__Endpoint = "http://localhost:11434"

        示例:
          dotnet run                    # 运行所有演示
          dotnet run --agent            # 只运行 Agent 演示
          dotnet run -i                 # 交互式对话模式
        """
    );
}

static ILLMProvider? GetProvider(IServiceProvider services)
{
    try
    {
        var provider = services.GetRequiredService<ILLMProvider>();
        PrintSuccess($"已创建 {provider.Name} 提供者");
        return provider;
    }
    catch (Exception ex)
    {
        PrintError($"创建提供者失败: {ex.Message}");
        Console.WriteLine("请检查 appsettings.json 配置，参考 CONFIG.md");
        return null;
    }
}

// ============================================================================
// 演示方法
// ============================================================================

static async Task RunChatDemo(ILLMProvider provider)
{
    PrintSection("1. 简单聊天");
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
        PrintDim(
            $"Token: 输入={response.PromptTokens}, 输出={response.CompletionTokens}, 总计={response.TotalTokens}"
        );
    }
    catch (Exception ex)
    {
        PrintError($"请求失败: {ex.Message}");
    }

    Console.WriteLine();
}

static async Task RunAgentDemo(IAgent agent)
{
    PrintSection("2. Agent 演示（ReAct 模式）");
    Console.WriteLine($"✓ Agent: {agent.Name}\n");

    var question =
        "帮我搜索 AI Agent 的常见架构模式，然后计算如果一个 Agent 系统有 3 个专家 Agent，每个专家有 4 个工具，总共需要多少个工具调用能力？最后总结多 Agent 协作的优势。";
    Console.WriteLine($"📝 问题：{question}\n");

    var response = await agent.RunAsync(question);

    // 执行过程
    PrintDivider("🔄 执行过程");

    foreach (var step in response.Steps)
    {
        Console.WriteLine($"\n【步骤 {step.StepNumber}】");

        if (!string.IsNullOrEmpty(step.Thought))
        {
            PrintColored($"  💭 思考：{step.Thought.Trim()}", ConsoleColor.Cyan);
        }

        if (!string.IsNullOrEmpty(step.Action))
        {
            PrintColored($"  🎯 动作：{step.Action}", ConsoleColor.Yellow);
            PrintColored($"  📥 输入：{step.ActionInput}", ConsoleColor.Yellow);
        }

        if (!string.IsNullOrEmpty(step.Observation))
        {
            PrintColored($"  👁️ 观察：{step.Observation.Trim()}", ConsoleColor.Green);
        }
    }

    // 执行摘要
    PrintDivider("📊 执行摘要");
    Console.WriteLine($"  状态：{(response.Success ? "✅ 成功" : "❌ 失败")}");
    Console.WriteLine($"  步骤：{response.Steps.Count} 步");
    Console.WriteLine($"  耗时：{response.Duration.TotalMilliseconds:F0}ms");

    if (response.Success && !string.IsNullOrEmpty(response.FinalAnswer))
    {
        PrintColored($"\n  📝 总结：{response.FinalAnswer}", ConsoleColor.Magenta);
    }

    if (!response.Success && !string.IsNullOrEmpty(response.Error))
    {
        PrintError($"  错误：{response.Error}");
    }

    Console.WriteLine();
}

static async Task RunStreamDemo(ILLMProvider provider)
{
    PrintSection("3. 流式聊天");
    Console.WriteLine("问题：AI Agent 常用的工具类型有哪些？\n");
    Console.Write("回复：");

    await foreach (
        var chunk in provider.ChatStreamAsync(
            [new ChatMessage("user", "列举 AI Agent 常用的 5 种工具类型，每种用一句话说明用途。")],
            new ChatCompletionOptions { MaxTokens = 400 }
        )
    )
    {
        Console.Write(chunk);
    }

    Console.WriteLine("\n");
}

static async Task RunInteractiveChat(ILLMProvider provider)
{
    PrintSection("4. 交互式对话");
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

// ============================================================================
// 输出辅助
// ============================================================================

static void PrintSection(string title)
{
    Console.WriteLine($"━━━ {title} ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
}

static void PrintDivider(string title)
{
    Console.WriteLine($"\n┌─ {title} ─────────────────────────────────────────────┐");
}

static void PrintSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ {message}");
    Console.ResetColor();
}

static void PrintError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ResetColor();
}

static void PrintDim(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(message);
    Console.ResetColor();
}

static void PrintColored(string message, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ResetColor();
}

// ============================================================================
// 枚举
// ============================================================================

enum RunMode
{
    All,
    Chat,
    Agent,
    Stream,
    Interactive,
}
