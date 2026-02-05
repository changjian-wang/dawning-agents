using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.Core.Tools.BuiltIn;
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
        // 注册内置工具
        services.AddBuiltInTools();

        // 注册 ReAct Agent
        services.AddReActAgent(options =>
        {
            options.Name = "HelloAgent";
            options.Instructions = "你是一个友好的 AI 助手，擅长回答问题和使用工具。";
            options.MaxSteps = 5;
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

        // 示例 3: 工具使用
        await RunToolUsageAsync();
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
        ConsoleHelper.PrintInfo($"可用工具数: {registry.GetAllTools().Count}");
        Console.WriteLine();

        // 简单问答
        var question = "你好！请简单介绍一下你自己。";
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
    /// 示例 3: 工具使用 - Agent 调用工具
    /// </summary>
    private async Task RunToolUsageAsync()
    {
        ConsoleHelper.PrintTitle("示例 3: 工具使用");
        ConsoleHelper.PrintStep(1, "让 Agent 使用内置工具");

        var agent = GetService<IAgent>();
        var registry = GetService<IToolRegistry>();
        var tools = registry.GetAllTools();

        // 列出可用工具
        ConsoleHelper.PrintInfo("可用工具:");
        foreach (var tool in tools.Take(5))
        {
            ConsoleHelper.PrintDim($"  - {tool.Name}: {tool.Description}");
        }
        if (tools.Count > 5)
        {
            ConsoleHelper.PrintDim($"  ... 还有 {tools.Count - 5} 个工具");
        }
        Console.WriteLine();

        // 使用工具的问题
        var question = "现在是几点？今天是星期几？";
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
