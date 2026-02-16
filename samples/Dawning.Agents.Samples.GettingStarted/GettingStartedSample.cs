using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Samples.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Samples.GettingStarted;

/// <summary>
/// 入门示例 - 展示 Dawning.Agents 的基础用法
/// </summary>
public class GettingStartedSample : SampleBase
{
    protected override string SampleName => "Getting Started";

    protected override void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 注册 6 个核心工具 (read_file, write_file, edit_file, search, bash, create_tool)
        services.AddCoreTools();

        // 注册 Function Calling Agent (推荐，比 ReAct 更可靠)
        services.AddFunctionCallingAgent(options =>
        {
            options.Name = "HelloAgent";
            options.Instructions =
                "你是一个友好的 AI 助手，擅长回答问题和使用工具。"
                + "你可以使用 bash 执行命令、read_file 读取文件、search 搜索代码。"
                + "如果需要重复执行某个操作，可以用 create_tool 创建可复用的工具。";
            options.MaxSteps = 10;
        });
    }

    protected override async Task ExecuteAsync()
    {
        // 示例 1: Hello Agent
        await RunHelloAgentAsync();

        ConsoleHelper.WaitForKey();

        // 示例 2: 简单聊天
        await RunSimpleChatAsync();

        ConsoleHelper.WaitForKey();

        // 示例 3: 核心工具使用
        await RunCoreToolsAsync();
    }

    /// <summary>
    /// 示例 1: Hello Agent - 最简单的 Agent 调用
    /// </summary>
    private async Task RunHelloAgentAsync()
    {
        ConsoleHelper.PrintTitle("示例 1: Hello Agent");
        ConsoleHelper.PrintStep(1, "创建并运行最简单的 Agent");

        var agent = GetService<IAgent>();
        var registry = GetService<IToolRegistry>();

        ConsoleHelper.PrintInfo($"Agent 名称: {agent.Name}");

        // 列出核心工具
        var tools = registry.GetAllTools();
        ConsoleHelper.PrintInfo($"核心工具 ({tools.Count} 个):");
        foreach (var tool in tools)
        {
            ConsoleHelper.PrintDim(
                $"  - {tool.Name}: {tool.Description[..Math.Min(60, tool.Description.Length)]}..."
            );
        }
        Console.WriteLine();

        // 简单问答
        var question = "你好！请简单介绍一下你自己，以及你有哪些工具可以使用。";
        ConsoleHelper.PrintDim($"用户: {question}");

        var response = await agent.RunAsync(question);

        Console.WriteLine();
        ConsoleHelper.PrintColored($"Agent: {response.FinalAnswer}", ConsoleColor.Green);
    }

    /// <summary>
    /// 示例 2: 简单聊天 - 直接使用 LLM Provider
    /// </summary>
    private async Task RunSimpleChatAsync()
    {
        ConsoleHelper.PrintTitle("示例 2: 简单聊天");
        ConsoleHelper.PrintStep(1, "直接使用 ILLMProvider 进行对话");

        var provider = GetService<ILLMProvider>();

        var messages = new List<ChatMessage>
        {
            new("system", "你是一个专业的编程助手。"),
            new("user", "用一句话解释什么是依赖注入？"),
        };

        ConsoleHelper.PrintDim("用户: 用一句话解释什么是依赖注入？");
        Console.WriteLine();

        var response = await provider.ChatAsync(messages);

        ConsoleHelper.PrintColored($"LLM: {response.Content}", ConsoleColor.Cyan);
    }

    /// <summary>
    /// 示例 3: 核心工具使用 - Agent 调用核心工具
    /// </summary>
    private async Task RunCoreToolsAsync()
    {
        ConsoleHelper.PrintTitle("示例 3: 核心工具使用");
        ConsoleHelper.PrintStep(1, "让 Agent 使用核心工具完成任务");

        var agent = GetService<IAgent>();

        // 使用 bash 工具
        var question = "请用 bash 执行 'echo Hello from Dawning.Agents && date' 并告诉我结果。";
        ConsoleHelper.PrintDim($"用户: {question}");
        Console.WriteLine();

        var response = await agent.RunAsync(question);

        // 显示推理步骤
        if (response.Steps.Count > 0)
        {
            ConsoleHelper.PrintInfo($"推理步骤 ({response.Steps.Count} 步):");
            foreach (var step in response.Steps)
            {
                ConsoleHelper.PrintDim($"  [{step.StepNumber}] {step.Thought ?? step.Action}");
                if (!string.IsNullOrEmpty(step.Action))
                {
                    ConsoleHelper.PrintDim($"      动作: {step.Action}");
                }
            }
            Console.WriteLine();
        }

        ConsoleHelper.PrintColored($"Agent: {response.FinalAnswer}", ConsoleColor.Green);
    }
}
