using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Orchestration;
using Dawning.Agents.Core.Orchestration;
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

        // ====================================================================
        // 1. 顺序编排器演示
        // ====================================================================
        ConsoleHelper.PrintDivider("1️⃣ 顺序编排器 (Sequential)");
        Console.WriteLine("场景：文本处理流水线 - 提取关键词 → 情感分析 → 生成摘要\n");

        // 创建模拟 Agent - 每个 Agent 处理不同任务，输出完全不同的内容
        var keywordExtractor = new MockAgent(
            "关键词提取",
            async (input, ct) =>
            {
                await Task.Delay(100, ct);
                // 模拟提取关键词
                return "关键词: AI, 机器学习, 深度学习, 神经网络, 自然语言处理";
            }
        );

        var sentimentAnalyzer = new MockAgent(
            "情感分析",
            async (input, ct) =>
            {
                await Task.Delay(100, ct);
                // 基于关键词进行情感分析
                return "情感: 积极 (85%) | 主题: 技术创新 | 领域: 人工智能";
            }
        );

        var summaryGenerator = new MockAgent(
            "摘要生成",
            async (input, ct) =>
            {
                await Task.Delay(100, ct);
                // 基于前面的分析生成摘要
                return "📝 摘要: 这是一篇关于人工智能技术的积极正面文章，涵盖了机器学习和深度学习等核心技术。";
            }
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

        var legalExpert = new MockAgent(
            "法律专家",
            async (input, ct) =>
            {
                await Task.Delay(150, ct);
                return "从法律角度看，建议重点关注合同条款和合规性问题。";
            }
        );

        var techExpert = new MockAgent(
            "技术专家",
            async (input, ct) =>
            {
                await Task.Delay(120, ct);
                return "从技术角度看，需要评估实施可行性和技术风险。";
            }
        );

        var financeExpert = new MockAgent(
            "财务专家",
            async (input, ct) =>
            {
                await Task.Delay(100, ct);
                return "从财务角度看，ROI 预计为 150%，回收周期约 18 个月。";
            }
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
        // 4. 统计信息
        // ====================================================================
        ConsoleHelper.PrintDivider("📊 编排器能力总结");
        Console.WriteLine("  ✅ SequentialOrchestrator - 流水线处理，前一个输出→后一个输入");
        Console.WriteLine("  ✅ ParallelOrchestrator - 并行执行，支持多种聚合策略");
        Console.WriteLine("  ✅ 聚合策略: LastResult, FirstSuccess, Merge, Vote, Custom");
        Console.WriteLine("  ✅ 支持超时控制、错误处理、并发限制");
        Console.WriteLine("  ✅ 完整的执行记录和追踪");
    }
}

/// <summary>
/// 用于演示的模拟 Agent
/// </summary>
public class MockAgent : IAgent
{
    private readonly Func<string, CancellationToken, Task<string>> _handler;

    public MockAgent(string name, Func<string, CancellationToken, Task<string>> handler)
    {
        Name = name;
        _handler = handler;
    }

    public string Name { get; }
    public string Instructions => $"Mock Agent: {Name}";

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await _handler(input, cancellationToken);
            stopwatch.Stop();
            return AgentResponse.Successful(result, [], stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return AgentResponse.Failed(ex.Message, [], stopwatch.Elapsed);
        }
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}
