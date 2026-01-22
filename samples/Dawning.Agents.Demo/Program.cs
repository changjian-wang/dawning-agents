using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
using Dawning.Agents.Demo;
using Dawning.Agents.Demo.Demos;
using Dawning.Agents.Demo.Helpers;
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

ConsoleHelper.PrintBanner();

// 如果没有指定模式，显示交互式菜单
if (runMode == RunMode.Menu)
{
    runMode = ShowMenu();
    if (runMode == RunMode.Menu)
    {
        Console.WriteLine("\n再见！");
        return;
    }
}

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
        await ChatDemos.RunChatDemo(provider);
        break;
    case RunMode.Agent:
        await AgentDemos.RunAgentDemo(agent);
        break;
    case RunMode.Stream:
        await ChatDemos.RunStreamDemo(provider);
        break;
    case RunMode.Interactive:
        await ChatDemos.RunInteractiveChat(provider);
        break;
    case RunMode.Memory:
        var memory = host.Services.GetRequiredService<IConversationMemory>();
        var tokenCounter = host.Services.GetRequiredService<ITokenCounter>();
        await MemoryDemos.RunMemoryDemo(provider, memory, tokenCounter);
        break;
    case RunMode.AgentMemory:
        var agentMemory = host.Services.GetRequiredService<IConversationMemory>();
        await AgentDemos.RunAgentMemoryDemo(agent, agentMemory);
        break;
    case RunMode.PackageManager:
        var registry = host.Services.GetRequiredService<IToolRegistry>();
        await ToolDemos.RunPackageManagerDemo(registry);
        break;
    case RunMode.Orchestrator:
        await OrchestratorDemos.RunOrchestratorDemo(provider);
        break;
    case RunMode.Handoff:
        await HandoffDemos.RunHandoffDemo(provider);
        break;
    case RunMode.All:
        await ChatDemos.RunChatDemo(provider);
        await AgentDemos.RunAgentDemo(agent);
        await ChatDemos.RunStreamDemo(provider);
        break;
    default:
        break;
}

Console.WriteLine("\n再见！");

// ============================================================================
// 辅助方法
// ============================================================================

static (bool showHelp, RunMode mode) ParseArgs(string[] args)
{
    var showHelp = args.Contains("--help") || args.Contains("-h");
    var mode = RunMode.Menu; // 默认显示菜单

    if (args.Contains("--all"))
    {
        mode = RunMode.All;
    }
    else if (args.Contains("--chat"))
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
    else if (args.Contains("--orchestrator") || args.Contains("-o"))
    {
        mode = RunMode.Orchestrator;
    }
    else if (args.Contains("--handoff") || args.Contains("-hf"))
    {
        mode = RunMode.Handoff;
    }

    return (showHelp, mode);
}

static RunMode ShowMenu()
{
    Console.WriteLine("请选择要运行的演示：\n");
    Console.WriteLine("  [1] 简单聊天          - LLM 基础对话");
    Console.WriteLine("  [2] Agent 演示        - ReAct 模式工具调用");
    Console.WriteLine("  [3] 流式聊天          - 流式输出演示");
    Console.WriteLine("  [4] 交互式对话        - 多轮对话模式");
    Console.WriteLine("  [5] Memory 系统       - 滑动窗口记忆");
    Console.WriteLine("  [6] Agent + Memory    - Agent 多轮对话");
    Console.WriteLine("  [7] 包管理工具        - PackageManagerTool");
    Console.WriteLine("  [8] 多 Agent 编排器   - Orchestrator 演示");
    Console.WriteLine("  [9] Handoff 协作      - Agent 任务转交");
    Console.WriteLine("  [A] 运行全部          - 依次运行 1-3");
    Console.WriteLine("  [Q] 退出");
    Console.WriteLine();
    Console.Write("请输入选项 (1-9/A/Q): ");

    var input = Console.ReadLine()?.Trim().ToUpperInvariant();

    return input switch
    {
        "1" => RunMode.Chat,
        "2" => RunMode.Agent,
        "3" => RunMode.Stream,
        "4" => RunMode.Interactive,
        "5" => RunMode.Memory,
        "6" => RunMode.AgentMemory,
        "7" => RunMode.PackageManager,
        "8" => RunMode.Orchestrator,
        "9" => RunMode.Handoff,
        "A" => RunMode.All,
        "Q" or "" or null => RunMode.Menu, // 返回 Menu 表示退出
        _ => RunMode.Menu,
    };
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
          -o, --orchestrator  演示多 Agent 编排器
          -hf, --handoff  演示 Handoff Agent 协作
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
          dotnet run                    # 显示菜单
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
        ConsoleHelper.PrintSuccess($"已创建 {provider.Name} 提供者");
        return provider;
    }
    catch (Exception ex)
    {
        ConsoleHelper.PrintError($"创建提供者失败: {ex.Message}");
        Console.WriteLine("请检查 appsettings.json 配置，参考 CONFIG.md");
        return null;
    }
}
