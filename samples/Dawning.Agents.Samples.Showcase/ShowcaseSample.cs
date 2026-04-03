using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.HumanLoop;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.Safety;
using Dawning.Agents.Core;
using Dawning.Agents.Core.HumanLoop;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Safety;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Samples.Common;
using Dawning.Agents.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Samples.Showcase;

/// <summary>
/// 综合示例 — 展示框架所有核心功能
/// </summary>
public class ShowcaseSample : SampleBase
{
    protected override string SampleName => "Showcase (All-in-One)";

    protected override void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 核心工具（read_file, write_file, edit_file, search, bash, create_tool）
        services.AddCoreTools();

        // FunctionCallingAgent（推荐的 Agent 类型）
        services.AddFunctionCallingAgent(options =>
        {
            options.Name = "ShowcaseAgent";
            options.Instructions = "你是一个功能展示助手，帮助演示框架的各种能力。";
            options.MaxSteps = 10;
        });

        // 记忆系统（从配置读取类型）
        services.AddMemory(configuration);

        // 安全护栏
        services.AddSafetyGuardrails(configuration);

        // 人机协作
        services.AddHumanLoop();
        services.AddSingleton<IHumanInteractionHandler, ConsoleApprovalHandler>();

        // SQLite 持久化记忆
        services.AddSqliteMemory(options =>
        {
            options.ConnectionString = "Data Source=showcase_demo.db";
            options.AutoCreateSchema = true;
        });
    }

    protected override async Task ExecuteAsync()
    {
        while (true)
        {
            PrintMenu();
            var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(choice) || choice == "Q")
            {
                break;
            }

            try
            {
                await RunDemoAsync(choice);
            }
            catch (Exception ex)
            {
                ConsoleHelper.PrintError($"演示出错: {ex.Message}");
            }

            Console.WriteLine();
            ConsoleHelper.PrintDim("按回车继续...");
            Console.ReadLine();
        }
    }

    private static void PrintMenu()
    {
        Console.Clear();
        ConsoleHelper.PrintTitle("选择功能演示");
        Console.WriteLine("  [ 1] Agent 基础 — FunctionCalling + ReAct");
        Console.WriteLine("  [ 2] 工具系统 — 内置工具 + 自定义工具 + [FunctionTool]");
        Console.WriteLine("  [ 3] 记忆系统 — Buffer / Window / Summary / Adaptive");
        Console.WriteLine("  [ 4] 持久化记忆 — SQLite 会话持久化");
        Console.WriteLine("  [ 5] RAG 检索增强 — 文档分块 + 向量搜索");
        Console.WriteLine("  [ 6] 安全护栏 — 内容过滤 + 敏感数据 + 注入防护");
        Console.WriteLine("  [ 7] 人机协作 — 审批工作流");
        Console.WriteLine("  [ 8] 多 Agent 编排 — 顺序 + 并行 + Handoff");
        Console.WriteLine("  [ 9] 工作流引擎 — DSL 构建 + 执行");
        Console.WriteLine("  [10] 弹性与扩展 — 熔断器 + 负载均衡 + 特性开关");
        Console.WriteLine("  [11] Prompt 模板 — 变量替换引擎");
        Console.WriteLine("  [12] 评估框架 — 指标评估 + 测试集");
        Console.WriteLine("  [ A] 运行全部");
        Console.WriteLine("  [ Q] 退出");
        Console.WriteLine();
        Console.Write("请选择 (1-12/A/Q): ");
    }

    private async Task RunDemoAsync(string choice)
    {
        var demos = new Dictionary<string, Func<Task>>
        {
            ["1"] = () => new Demos.AgentBasicsDemo(Services).RunAsync(),
            ["2"] = () => new Demos.ToolSystemDemo(Services).RunAsync(),
            ["3"] = () => new Demos.MemoryDemo(Services).RunAsync(),
            ["4"] = () => new Demos.SqliteMemoryDemo(Services).RunAsync(),
            ["5"] = () => new Demos.RagDemo().RunAsync(),
            ["6"] = () => new Demos.SafetyDemo(Services).RunAsync(),
            ["7"] = () => new Demos.HumanLoopDemo(Services).RunAsync(),
            ["8"] = () => new Demos.OrchestrationDemo(Services).RunAsync(),
            ["9"] = () => new Demos.WorkflowDemo().RunAsync(),
            ["10"] = () => new Demos.ResilienceDemo().RunAsync(),
            ["11"] = () => new Demos.PromptTemplateDemo().RunAsync(),
            ["12"] = () => new Demos.EvaluationDemo().RunAsync(),
        };

        if (choice == "A")
        {
            foreach (var (key, demo) in demos.OrderBy(x => int.Parse(x.Key)))
            {
                ConsoleHelper.PrintTitle($"=== 演示 {key} ===");
                await demo();
                Console.WriteLine();
            }

            return;
        }

        if (demos.TryGetValue(choice, out var selectedDemo))
        {
            await selectedDemo();
        }
        else
        {
            ConsoleHelper.PrintWarning("无效选择");
        }
    }
}
