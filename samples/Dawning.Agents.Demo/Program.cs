using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
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
builder.Services.AddPackageManagerTools(options =>
{
    // 安全配置：白名单模式
    options.WhitelistedPackages = ["Git.*", "Microsoft.*", "Python.*", "nodejs", "dotnet-*"];
    options.BlacklistedPackages = ["*hack*", "*crack*", "*malware*"];
});

// 注册 Memory 服务
builder.Services.AddWindowMemory(windowSize: 6);

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
    case RunMode.Memory:
        var memory = host.Services.GetRequiredService<IConversationMemory>();
        var tokenCounter = host.Services.GetRequiredService<ITokenCounter>();
        await RunMemoryDemo(provider, memory, tokenCounter);
        break;
    case RunMode.AgentMemory:
        var agentMemory = host.Services.GetRequiredService<IConversationMemory>();
        await RunAgentMemoryDemo(agent, agentMemory);
        break;
    case RunMode.PackageManager:
        var registry = host.Services.GetRequiredService<IToolRegistry>();
        await RunPackageManagerDemo(registry);
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
    else if (args.Contains("--memory") || args.Contains("-m"))
    {
        mode = RunMode.Memory;
    }
    else if (args.Contains("--agent-memory") || args.Contains("-am"))
    {
        mode = RunMode.AgentMemory;
    }
    else if (args.Contains("--package-manager") || args.Contains("-pm"))
    {
        mode = RunMode.PackageManager;
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
          -m, --memory    演示 Memory 系统（滑动窗口记忆）
          -am, --agent-memory  演示 Agent + Memory 多轮对话
          -pm, --package-manager  演示 PackageManagerTool 包管理工具
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

static async Task RunMemoryDemo(
    ILLMProvider provider,
    IConversationMemory memory,
    ITokenCounter tokenCounter
)
{
    PrintSection("5. Memory 系统演示（滑动窗口）");

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
        await memory.AddMessageAsync(new ConversationMessage { Role = "user", Content = input });

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
        PrintDim($"  [消息数: {memory.MessageCount}, Token: ~{tokenCount}]");
        Console.WriteLine();
    }

    // 退出前显示最终状态
    Console.WriteLine("\n📊 最终记忆状态：");
    await PrintMemoryStatus(memory);
}

static async Task PrintMemoryStatus(IConversationMemory memory)
{
    var messages = await memory.GetMessagesAsync();
    var tokenCount = await memory.GetTokenCountAsync();

    PrintDivider("📝 记忆状态");
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
            PrintDim($"    {role} {preview.Replace("\n", " ")}");
        }
    }

    Console.WriteLine();
}

// ============================================================================
// 输出辅助
// ============================================================================

static void PrintTitle(string title)
{
    Console.WriteLine($"\n╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  {title, -58} ║");
    Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝\n");
}

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

static void PrintInfo(string message)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"ℹ {message}");
    Console.ResetColor();
}

static void PrintWarning(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"⚠ {message}");
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

static async Task RunAgentMemoryDemo(IAgent agent, IConversationMemory memory)
{
    PrintSection("Agent + Memory 多轮对话演示");
    Console.WriteLine($"✓ Agent: {agent.Name}");
    Console.WriteLine($"✓ Memory 类型: {memory.GetType().Name}");
    Console.WriteLine("\n演示 Agent 如何在多轮对话中自动保存记忆...\n");

    // 预设的多轮对话问题
    var questions = new[] { "计算 15 + 27 等于多少？", "再把刚才的结果乘以 2", "今天是几号？" };

    foreach (var question in questions)
    {
        PrintDivider($"📝 问题：{question}");

        var response = await agent.RunAsync(question);

        // 显示执行步骤
        foreach (var step in response.Steps)
        {
            if (!string.IsNullOrEmpty(step.Action))
            {
                PrintColored($"  🎯 {step.Action}({step.ActionInput})", ConsoleColor.Yellow);
                PrintColored($"  👁️ {step.Observation?.Trim()}", ConsoleColor.Green);
            }
        }

        if (response.Success && !string.IsNullOrEmpty(response.FinalAnswer))
        {
            PrintColored($"\n  💬 回答：{response.FinalAnswer}\n", ConsoleColor.Cyan);
        }

        // 显示 Memory 状态
        var messages = await memory.GetMessagesAsync();
        PrintDim($"  📚 Memory 状态: {messages.Count} 条消息");

        // 显示最近的消息摘要
        var recent = messages.TakeLast(4).ToList();
        foreach (var msg in recent)
        {
            var role = msg.Role == "user" ? "👤" : "🤖";
            var content = msg.Content.Length > 50 ? msg.Content[..50] + "..." : msg.Content;
            PrintDim($"     {role} {content}");
        }

        Console.WriteLine();
    }

    // 最终统计
    PrintDivider("📊 Memory 统计");
    var allMessages = await memory.GetMessagesAsync();
    Console.WriteLine($"  总消息数: {allMessages.Count}");
    Console.WriteLine($"  用户消息: {allMessages.Count(m => m.Role == "user")}");
    Console.WriteLine($"  助手消息: {allMessages.Count(m => m.Role == "assistant")}");
    var totalTokens = await memory.GetTokenCountAsync();
    Console.WriteLine($"  估计 Token: {totalTokens}");
}

/// <summary>
/// 演示 PackageManagerTool 包管理工具
/// </summary>
static async Task RunPackageManagerDemo(IToolRegistry registry)
{
    PrintTitle("📦 PackageManagerTool 演示");

    // 获取所有 PackageManager 类别的工具
    var pmTools = registry.GetToolsByCategory("PackageManager").ToList();

    Console.WriteLine($"\n已注册的包管理工具 ({pmTools.Count} 个):\n");

    // 按包管理器类型分组显示
    var wingetTools = pmTools.Where(t => t.Name.StartsWith("Winget")).ToList();
    var pipTools = pmTools.Where(t => t.Name.StartsWith("Pip")).ToList();
    var npmTools = pmTools.Where(t => t.Name.StartsWith("Npm")).ToList();
    var dotnetTools = pmTools.Where(t => t.Name.StartsWith("DotnetTool")).ToList();

    void PrintToolGroup(string groupName, string icon, IList<ITool> tools)
    {
        Console.WriteLine($"  {icon} {groupName} ({tools.Count} 个工具):");
        foreach (var tool in tools)
        {
            var riskIcon = tool.RiskLevel switch
            {
                ToolRiskLevel.Low => "🟢",
                ToolRiskLevel.Medium => "🟡",
                ToolRiskLevel.High => "🔴",
                _ => "⚪",
            };
            var confirmIcon = tool.RequiresConfirmation ? "🔒" : "";
            Console.WriteLine($"      {riskIcon} {tool.Name} {confirmIcon}");
            PrintDim($"         {tool.Description[..Math.Min(60, tool.Description.Length)]}...");
        }
        Console.WriteLine();
    }

    PrintToolGroup("Winget (Windows)", "🪟", wingetTools);
    PrintToolGroup("Pip (Python)", "🐍", pipTools);
    PrintToolGroup("Npm (Node.js)", "📦", npmTools);
    PrintToolGroup("Dotnet Tool (.NET)", "🔷", dotnetTools);

    // 演示工具执行
    PrintDivider("📋 工具演示");

    Console.WriteLine("\n1️⃣ 演示 DotnetToolList (安全只读操作):\n");
    var dotnetListTool = pmTools.FirstOrDefault(t => t.Name == "DotnetToolList");
    if (dotnetListTool != null)
    {
        PrintInfo($"执行 {dotnetListTool.Name}...");
        var result = await dotnetListTool.ExecuteAsync("{\"global\": true}");
        if (result.Success)
        {
            PrintSuccess("执行成功:");
            // 只显示前 10 行
            var lines = result.Output.Split('\n').Take(15);
            foreach (var line in lines)
            {
                Console.WriteLine($"  {line}");
            }
            if (result.Output.Split('\n').Length > 15)
            {
                PrintDim("  ... (更多输出已省略)");
            }
        }
        else
        {
            PrintError($"执行失败: {result.Error}");
        }
    }

    Console.WriteLine("\n2️⃣ 演示 DotnetToolSearch (安全只读操作):\n");
    var dotnetSearchTool = pmTools.FirstOrDefault(t => t.Name == "DotnetToolSearch");
    if (dotnetSearchTool != null)
    {
        PrintInfo("搜索 'dotnet-ef'...");
        var result = await dotnetSearchTool.ExecuteAsync("{\"query\": \"dotnet-ef\"}");
        if (result.Success)
        {
            PrintSuccess("搜索结果:");
            var lines = result.Output.Split('\n').Take(10);
            foreach (var line in lines)
            {
                Console.WriteLine($"  {line}");
            }
        }
        else
        {
            PrintError($"搜索失败: {result.Error}");
        }
    }

    Console.WriteLine("\n3️⃣ 高风险操作演示 (模拟):\n");
    PrintWarning("以下操作标记为高风险，实际执行时需要用户确认：");

    var highRiskTools = pmTools.Where(t => t.RiskLevel == ToolRiskLevel.High).Take(5);
    foreach (var tool in highRiskTools)
    {
        Console.WriteLine($"  🔴 {tool.Name}");
        PrintDim($"     {tool.Description[..Math.Min(70, tool.Description.Length)]}...");
    }

    // 统计信息
    PrintDivider("📊 统计信息");
    Console.WriteLine($"  总工具数: {pmTools.Count}");
    Console.WriteLine($"  低风险 (只读): {pmTools.Count(t => t.RiskLevel == ToolRiskLevel.Low)}");
    Console.WriteLine(
        $"  高风险 (需确认): {pmTools.Count(t => t.RiskLevel == ToolRiskLevel.High)}"
    );
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
    Memory,
    AgentMemory,
    PackageManager,
}
