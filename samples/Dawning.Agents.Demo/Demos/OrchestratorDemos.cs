using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Core.Orchestration;
using Dawning.Agents.Core.Telemetry;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// 编排器演示
/// </summary>
public static class OrchestratorDemos
{
    /// <summary>
    /// 多 Agent 编排器演示
    /// </summary>
    public static async Task RunOrchestratorDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintDivider("🎭 多 Agent 编排器演示");

        Console.WriteLine("\n编排器允许多个 Agent 协同工作：");
        Console.WriteLine("  • SequentialOrchestrator: 顺序执行（流水线）");
        Console.WriteLine("  • ParallelOrchestrator: 并行执行（多专家）\n");

        // 创建 Token 追踪器（使用框架提供的 InMemoryTokenUsageTracker）
        var tokenTracker = TokenStatsHelper.CreateTracker();

        // 创建带追踪功能的 LLM Provider 工厂方法
        TokenTrackingLLMProvider CreateTrackedProvider(string agentName) =>
            new(provider, tokenTracker, agentName);

        // ====================================================================
        // 1. 顺序编排器演示
        // ====================================================================
        ConsoleHelper.PrintDivider("1️⃣ 顺序编排器 (Sequential)");
        Console.WriteLine("场景：文本处理流水线 - 提取关键词 → 情感分析 → 生成摘要\n");

        // 创建 LLM Agent - 每个 Agent 处理不同任务
        var keywordExtractor = new SimpleLLMAgent(
            CreateTrackedProvider("关键词提取"),
            "关键词提取",
            "你是关键词提取专家。从用户输入的文本中提取5-8个关键词，用逗号分隔。只输出关键词，不要其他内容。格式：关键词: xxx, xxx, xxx"
        );

        var sentimentAnalyzer = new SimpleLLMAgent(
            CreateTrackedProvider("情感分析"),
            "情感分析",
            "你是情感分析专家。分析输入内容的情感倾向和主题。输出格式：情感: [积极/消极/中性] (百分比) | 主题: xxx | 领域: xxx"
        );

        var summaryGenerator = new SimpleLLMAgent(
            CreateTrackedProvider("摘要生成"),
            "摘要生成",
            "你是摘要生成专家。基于前面的分析结果，生成一句话摘要。格式：📝 摘要: xxx"
        );

        var sequentialOrchestrator = new SequentialOrchestrator("文本分析流水线")
            .AddAgent(keywordExtractor)
            .AddAgent(sentimentAnalyzer)
            .AddAgent(summaryGenerator);

        Console.WriteLine($"编排器: {sequentialOrchestrator.Name}");
        Console.WriteLine($"Agent 数量: {sequentialOrchestrator.Agents.Count}");
        Console.WriteLine(
            $"执行顺序: {string.Join(" → ", sequentialOrchestrator.Agents.Select(a => a.Name))}\n"
        );

        var input1 =
            "人工智能正在改变世界，机器学习和深度学习技术日新月异，神经网络在自然语言处理领域取得了突破性进展。";
        ConsoleHelper.PrintInfo($"原始文本: {input1}");
        Console.WriteLine();

        var result1 = await sequentialOrchestrator.RunAsync(input1);

        if (result1.Success)
        {
            Console.WriteLine("📋 执行详情:\n");
            foreach (var record in result1.AgentResults)
            {
                Console.WriteLine($"  [{record.ExecutionOrder + 1}] {record.AgentName}");
                ConsoleHelper.PrintColored(
                    $"      → {record.Response.FinalAnswer}",
                    ConsoleColor.Green
                );
                Console.WriteLine();
            }

            Console.WriteLine($"⏱️ 总耗时: {result1.Duration.TotalMilliseconds:F0}ms");
        }
        else
        {
            ConsoleHelper.PrintError($"执行失败: {result1.Error}");
        }

        // ====================================================================
        // 2. 并行编排器演示
        // ====================================================================
        ConsoleHelper.PrintDivider("2️⃣ 并行编排器 (Parallel)");
        Console.WriteLine("场景：多专家分析 - 同时询问多个专家并聚合意见\n");

        var legalExpert = new SimpleLLMAgent(
            CreateTrackedProvider("法律专家"),
            "法律专家",
            "你是企业法律顾问。从法律角度简短评估用户提出的项目，重点关注合同、合规和风险。一句话回答。"
        );

        var techExpert = new SimpleLLMAgent(
            CreateTrackedProvider("技术专家"),
            "技术专家",
            "你是技术架构师。从技术角度简短评估用户提出的项目，重点关注可行性和实施风险。一句话回答。"
        );

        var financeExpert = new SimpleLLMAgent(
            CreateTrackedProvider("财务专家"),
            "财务专家",
            "你是财务分析师。从财务角度简短评估用户提出的项目，预估ROI和回收周期。一句话回答。"
        );

        var parallelOrchestrator = new ParallelOrchestrator("专家委员会")
            .AddAgent(legalExpert)
            .AddAgent(techExpert)
            .AddAgent(financeExpert);

        Console.WriteLine($"编排器: {parallelOrchestrator.Name}");
        Console.WriteLine($"专家数量: {parallelOrchestrator.Agents.Count}");
        Console.WriteLine(
            $"专家列表: {string.Join(", ", parallelOrchestrator.Agents.Select(a => a.Name))}\n"
        );

        var input2 = "评估这个新项目的可行性";
        ConsoleHelper.PrintInfo($"问题: {input2}");

        var result2 = await parallelOrchestrator.RunAsync(input2);

        if (result2.Success)
        {
            ConsoleHelper.PrintSuccess($"聚合结果: {result2.FinalOutput}");
            Console.WriteLine($"总耗时: {result2.Duration.TotalMilliseconds:F0}ms (并行执行)\n");

            Console.WriteLine("各专家意见:");
            foreach (var record in result2.AgentResults.OrderBy(r => r.EndTime - r.StartTime))
            {
                var duration = (record.EndTime - record.StartTime).TotalMilliseconds;
                Console.WriteLine($"  🧑‍💼 {record.AgentName} ({duration:F0}ms):");
                ConsoleHelper.PrintDim($"      {record.Response.FinalAnswer}");
            }
        }

        // ====================================================================
        // 3. 自定义聚合策略
        // ====================================================================
        ConsoleHelper.PrintDivider("3️⃣ 自定义聚合策略");
        Console.WriteLine("使用 Merge 策略合并所有专家意见：\n");

        var customOrchestrator = new ParallelOrchestrator(
            "专家委员会-Merge",
            Microsoft.Extensions.Options.Options.Create(
                new OrchestratorOptions { AggregationStrategy = ResultAggregationStrategy.Merge }
            )
        )
            .AddAgent(legalExpert)
            .AddAgent(techExpert)
            .AddAgent(financeExpert);

        var result3 = await customOrchestrator.RunAsync(input2);

        if (result3.Success)
        {
            Console.WriteLine("合并后的完整报告:\n");
            Console.WriteLine(result3.FinalOutput);
        }

        // ====================================================================
        // 4. Token 统计（使用框架追踪器）
        // ====================================================================
        TokenStatsHelper.PrintSummary(tokenTracker);

        // ====================================================================
        // 5. 能力总结
        // ====================================================================
        ConsoleHelper.PrintDivider("📊 编排器能力总结");
        Console.WriteLine("  ✅ SequentialOrchestrator - 流水线处理，前一个输出→后一个输入");
        Console.WriteLine("  ✅ ParallelOrchestrator - 并行执行，支持多种聚合策略");
        Console.WriteLine("  ✅ 聚合策略: LastResult, FirstSuccess, Merge, Vote, Custom");
        Console.WriteLine("  ✅ 支持超时控制、错误处理、并发限制");
        Console.WriteLine("  ✅ 完整的执行记录和追踪");
    }
}
