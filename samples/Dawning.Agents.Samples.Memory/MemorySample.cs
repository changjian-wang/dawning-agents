using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Abstractions.Memory;
using Dawning.Agents.Abstractions.RAG;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.RAG;
using Dawning.Agents.Samples.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dawning.Agents.Samples.Memory;

/// <summary>
/// Memory 策略示例 - 展示五种上下文管理策略
/// </summary>
public class MemorySample : SampleBase
{
    protected override string SampleName => "Memory Strategies";

    protected override void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        // 注册 Token 计数器
        services.AddTokenCounter();

        // 注册 Embedding Provider (用于 VectorMemory)
        services.AddSingleton<IEmbeddingProvider, SimpleEmbeddingProvider>();

        // 注册 VectorStore (用于 VectorMemory)
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
    }

    protected override async Task ExecuteAsync()
    {
        // 选择要演示的 Memory 策略
        ConsoleHelper.PrintTitle("选择 Memory 策略");
        Console.WriteLine("  [1] BufferMemory   - 完整存储（短对话）");
        Console.WriteLine("  [2] WindowMemory   - 滑动窗口（控制 token）");
        Console.WriteLine("  [3] SummaryMemory  - LLM 摘要压缩（长对话）");
        Console.WriteLine("  [4] AdaptiveMemory - 自动降级（推荐）");
        Console.WriteLine("  [5] VectorMemory   - 向量检索增强");
        Console.WriteLine("  [A] 运行全部比较");
        Console.WriteLine();
        Console.Write("请选择 (1-5/A): ");

        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();

        switch (choice)
        {
            case "1":
                await RunBufferMemoryDemoAsync();
                break;
            case "2":
                await RunWindowMemoryDemoAsync();
                break;
            case "3":
                await RunSummaryMemoryDemoAsync();
                break;
            case "4":
                await RunAdaptiveMemoryDemoAsync();
                break;
            case "5":
                await RunVectorMemoryDemoAsync();
                break;
            case "A":
                await RunComparisonAsync();
                break;
            default:
                ConsoleHelper.PrintWarning("无效选择，运行 AdaptiveMemory 演示");
                await RunAdaptiveMemoryDemoAsync();
                break;
        }
    }

    /// <summary>
    /// BufferMemory 演示 - 完整存储所有消息
    /// </summary>
    private async Task RunBufferMemoryDemoAsync()
    {
        ConsoleHelper.PrintTitle("BufferMemory - 完整存储");
        ConsoleHelper.PrintInfo("特点：存储所有消息，不丢失任何上下文");
        ConsoleHelper.PrintWarning("适用：短对话（<10 轮）");
        Console.WriteLine();

        var tokenCounter = GetService<ITokenCounter>();
        var memory = new BufferMemory(tokenCounter);

        await SimulateConversationAsync(memory, "Buffer", 5);
    }

    /// <summary>
    /// WindowMemory 演示 - 滑动窗口
    /// </summary>
    private async Task RunWindowMemoryDemoAsync()
    {
        ConsoleHelper.PrintTitle("WindowMemory - 滑动窗口");
        ConsoleHelper.PrintInfo("特点：只保留最近 N 条消息，控制 token 使用");
        ConsoleHelper.PrintWarning("适用：中等对话，需要控制成本");
        Console.WriteLine();

        var tokenCounter = GetService<ITokenCounter>();
        var memory = new WindowMemory(tokenCounter, windowSize: 4);

        await SimulateConversationAsync(memory, "Window(4)", 8);
    }

    /// <summary>
    /// SummaryMemory 演示 - LLM 摘要
    /// </summary>
    private async Task RunSummaryMemoryDemoAsync()
    {
        ConsoleHelper.PrintTitle("SummaryMemory - LLM 摘要压缩");
        ConsoleHelper.PrintInfo("特点：自动用 LLM 压缩旧消息为摘要");
        ConsoleHelper.PrintWarning("适用：长对话（>10 轮），需要保留语义");
        Console.WriteLine();

        var provider = GetService<ILLMProvider>();
        var tokenCounter = GetService<ITokenCounter>();
        var memory = new SummaryMemory(
            provider,
            tokenCounter,
            maxRecentMessages: 4,
            summaryThreshold: 6
        );

        await SimulateConversationAsync(memory, "Summary", 10);
    }

    /// <summary>
    /// AdaptiveMemory 演示 - 自动降级
    /// </summary>
    private async Task RunAdaptiveMemoryDemoAsync()
    {
        ConsoleHelper.PrintTitle("AdaptiveMemory - 自动降级");
        ConsoleHelper.PrintInfo("特点：初始用 Buffer，超过阈值自动切换到 Summary");
        ConsoleHelper.PrintSuccess("推荐：生产环境首选，自动平衡性能和成本");
        Console.WriteLine();

        var provider = GetService<ILLMProvider>();
        var tokenCounter = GetService<ITokenCounter>();
        var memory = new AdaptiveMemory(
            provider,
            tokenCounter,
            downgradeThreshold: 500, // 低阈值便于演示
            maxRecentMessages: 4,
            summaryThreshold: 6
        );

        await SimulateConversationAsync(memory, "Adaptive", 10, showDowngrade: true);
    }

    /// <summary>
    /// VectorMemory 演示 - 向量检索增强
    /// </summary>
    private async Task RunVectorMemoryDemoAsync()
    {
        ConsoleHelper.PrintTitle("VectorMemory - 向量检索增强");
        ConsoleHelper.PrintInfo("特点：将历史存入向量库，按相关性检索");
        ConsoleHelper.PrintWarning("适用：超长对话，需要回忆特定上下文");
        Console.WriteLine();

        var vectorStore = GetService<IVectorStore>();
        var embeddingProvider = GetService<IEmbeddingProvider>();
        var tokenCounter = GetService<ITokenCounter>();

        var memory = new VectorMemory(
            vectorStore,
            embeddingProvider,
            tokenCounter,
            recentWindowSize: 4,
            retrieveTopK: 3,
            minRelevanceScore: 0.3f
        );

        await SimulateConversationAsync(memory, "Vector", 8);
    }

    /// <summary>
    /// 对比所有 Memory 策略
    /// </summary>
    private async Task RunComparisonAsync()
    {
        ConsoleHelper.PrintTitle("Memory 策略对比");

        var provider = GetService<ILLMProvider>();
        var tokenCounter = GetService<ITokenCounter>();
        var vectorStore = GetService<IVectorStore>();
        var embeddingProvider = GetService<IEmbeddingProvider>();

        // 创建所有 Memory 实例
        var memories = new Dictionary<string, IConversationMemory>
        {
            ["Buffer"] = new BufferMemory(tokenCounter),
            ["Window(4)"] = new WindowMemory(tokenCounter, windowSize: 4),
            ["Summary"] = new SummaryMemory(provider, tokenCounter, 4, 6),
            ["Adaptive"] = new AdaptiveMemory(provider, tokenCounter, 500, 4, 6),
            ["Vector"] = new VectorMemory(vectorStore, embeddingProvider, tokenCounter, 4, 3, 0.3f),
        };

        // 模拟相同的对话
        var messages = new[]
        {
            ("user", "我叫张三，是一名软件工程师"),
            ("assistant", "你好张三！很高兴认识你。"),
            ("user", "我在北京工作，专注于 .NET 开发"),
            ("assistant", "北京是个好地方，.NET 是很棒的技术栈！"),
            ("user", "我最近在学习 AI Agent"),
            ("assistant", "AI Agent 是当前热门领域，很有前景。"),
            ("user", "你还记得我叫什么名字吗？"),
            ("assistant", "你叫张三，是北京的 .NET 软件工程师。"),
        };

        // 向每个 Memory 添加消息
        foreach (var (_, memory) in memories)
        {
            foreach (var (role, content) in messages)
            {
                await memory.AddMessageAsync(new ConversationMessage { Role = role, Content = content });
            }
        }

        // 显示对比结果
        Console.WriteLine();
        ConsoleHelper.PrintSection("对比结果");
        Console.WriteLine();
        Console.WriteLine("| 策略      | 消息数 | Token 数 | 特点                    |");
        Console.WriteLine("|-----------|--------|----------|-------------------------|");

        foreach (var (name, memory) in memories)
        {
            var count = memory.MessageCount;
            var tokens = await memory.GetTokenCountAsync();
            var feature = name switch
            {
                "Buffer" => "完整存储",
                "Window(4)" => "只保留最近4条",
                "Summary" => "压缩+最近消息",
                "Adaptive" => "自动降级",
                "Vector" => "检索+最近消息",
                _ => "",
            };
            Console.WriteLine($"| {name,-9} | {count,6} | {tokens,8} | {feature,-23} |");
        }

        Console.WriteLine();
        ConsoleHelper.PrintSuccess("推荐: 生产环境使用 AdaptiveMemory");
    }

    /// <summary>
    /// 模拟对话
    /// </summary>
    private async Task SimulateConversationAsync(
        IConversationMemory memory,
        string memoryType,
        int rounds,
        bool showDowngrade = false
    )
    {
        var provider = GetService<ILLMProvider>();

        ConsoleHelper.PrintSection($"模拟 {rounds} 轮对话");
        Console.WriteLine();

        for (int i = 1; i <= rounds; i++)
        {
            var userMessage = $"这是第 {i} 条消息，请记住数字 {i * 100}";

            await memory.AddMessageAsync(
                new ConversationMessage { Role = "user", Content = userMessage }
            );

            var tokens = await memory.GetTokenCountAsync();

            ConsoleHelper.PrintDim($"[{i}/{rounds}] 用户: {userMessage}");
            ConsoleHelper.PrintDim(
                $"        消息数: {memory.MessageCount}, Token: {tokens}"
            );

            // 检查 AdaptiveMemory 是否已降级
            if (showDowngrade && memory is AdaptiveMemory adaptive && adaptive.HasDowngraded)
            {
                ConsoleHelper.PrintWarning("        ⚡ 已自动降级到 SummaryMemory!");
            }

            // 模拟 LLM 响应
            var assistantMessage = $"好的，我记住了数字 {i * 100}";
            await memory.AddMessageAsync(
                new ConversationMessage { Role = "assistant", Content = assistantMessage }
            );

            await Task.Delay(100); // 模拟延迟
        }

        Console.WriteLine();
        ConsoleHelper.PrintSection("最终状态");

        var finalTokens = await memory.GetTokenCountAsync();
        var context = await memory.GetContextAsync();

        Console.WriteLine($"  Memory 类型: {memoryType}");
        Console.WriteLine($"  消息数量: {memory.MessageCount}");
        Console.WriteLine($"  Token 总数: {finalTokens}");
        Console.WriteLine($"  上下文消息数: {context.Count}");
        Console.WriteLine();

        // 显示实际上下文
        ConsoleHelper.PrintInfo("实际传递给 LLM 的上下文:");
        foreach (var msg in context.TakeLast(4))
        {
            var preview =
                msg.Content.Length > 50 ? msg.Content[..50] + "..." : msg.Content;
            ConsoleHelper.PrintDim($"  [{msg.Role}] {preview}");
        }
        if (context.Count > 4)
        {
            ConsoleHelper.PrintDim($"  ... 还有 {context.Count - 4} 条消息");
        }
    }
}
