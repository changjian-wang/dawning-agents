using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.Handoff;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.Handoff;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Demo.Helpers;

namespace Dawning.Agents.Demo.Demos;

/// <summary>
/// Handoff 演示
/// </summary>
public static class HandoffDemos
{
    /// <summary>
    /// Handoff 多 Agent 协作演示
    /// </summary>
    public static async Task RunHandoffDemo(ILLMProvider provider)
    {
        ConsoleHelper.PrintDivider("🤝 Handoff 多 Agent 协作演示");

        Console.WriteLine("\nHandoff 允许 Agent 将任务转交给其他专家 Agent：");
        Console.WriteLine("  • Triage Agent: 分析请求并分配给专家");
        Console.WriteLine("  • 专家 Agent: 处理特定领域的问题\n");

        // Token 统计收集器
        var statsCollector = new HandoffTokenStats();

        // ====================================================================
        // 1. 创建 Handoff Handler 和 Agent
        // ====================================================================
        var handler = new HandoffHandler();

        // 创建 Triage Agent - 负责分析请求并分配
        var triageAgent = new TriageAgent(provider, statsCollector);

        // 创建专家 Agent
        var techExpert = new ExpertAgent(
            provider,
            statsCollector,
            "技术专家",
            "技术问题",
            "你是一位资深技术专家，擅长软件架构、系统设计、DevOps 和云原生技术。请提供专业、实用的技术建议。"
        );

        var legalExpert = new ExpertAgent(
            provider,
            statsCollector,
            "法律专家",
            "法律问题",
            "你是一位企业法律顾问，擅长合同法、知识产权和商业合规。请提供专业的法律建议（仅供参考，不构成法律意见）。"
        );

        var financeExpert = new ExpertAgent(
            provider,
            statsCollector,
            "财务专家",
            "财务问题",
            "你是一位财务分析专家，擅长投资回报分析、预算规划和风险评估。请提供专业的财务建议。"
        );

        // 注册所有 Agent
        handler.RegisterAgents([triageAgent, techExpert, legalExpert, financeExpert]);

        ConsoleHelper.PrintDivider("📋 已注册的 Agent");
        foreach (var agent in handler.GetAllAgents())
        {
            Console.WriteLine($"  • {agent.Name}: {agent.Instructions}");
        }

        // ====================================================================
        // 2. 演示 Handoff 流程
        // ====================================================================
        var testCases = new[]
        {
            ("技术问题", "我们的系统需要支持高并发，应该如何设计架构？"),
            ("法律问题", "我们需要和供应商签订合作协议，有哪些注意事项？"),
            ("财务问题", "这个新项目需要 500 万预算，如何评估投资回报？"),
        };

        foreach (var (category, question) in testCases)
        {
            ConsoleHelper.PrintDivider($"🎯 测试: {category}");
            ConsoleHelper.PrintInfo($"用户问题: {question}");
            Console.WriteLine();

            var result = await handler.RunWithHandoffAsync("Triage", question);

            if (result.Success)
            {
                // 显示 Handoff 链路
                Console.WriteLine("📍 Handoff 链路:");
                for (var i = 0; i < result.HandoffChain.Count; i++)
                {
                    var record = result.HandoffChain[i];
                    var from = record.FromAgent ?? "用户";
                    Console.WriteLine($"  [{i + 1}] {from} → {record.ToAgent}");
                    if (!string.IsNullOrEmpty(record.Reason))
                    {
                        ConsoleHelper.PrintDim($"      原因: {record.Reason}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"✅ 最终处理: {result.ExecutedByAgent}");
                ConsoleHelper.PrintColored(
                    $"💬 回答: {result.Response?.FinalAnswer}",
                    ConsoleColor.Green
                );
                ConsoleHelper.PrintDim($"⏱️ 耗时: {result.TotalDuration.TotalMilliseconds:F0}ms");
            }
            else
            {
                ConsoleHelper.PrintError($"❌ 失败: {result.Error}");
            }

            Console.WriteLine();
        }

        // ====================================================================
        // 3. 演示循环检测
        // ====================================================================
        ConsoleHelper.PrintDivider("🔄 Handoff 安全机制演示");
        Console.WriteLine("\n1️⃣ 循环检测 (Agent A → B → A):");

        var cycleHandler = new HandoffHandler();
        cycleHandler.RegisterAgent(new CyclicAgent("AgentA", "AgentB"));
        cycleHandler.RegisterAgent(new CyclicAgent("AgentB", "AgentA"));

        var cycleResult = await cycleHandler.RunWithHandoffAsync("AgentA", "Start");
        Console.WriteLine($"  结果: {(cycleResult.Success ? "成功" : "失败")}");
        if (!cycleResult.Success)
        {
            ConsoleHelper.PrintColored($"  检测到: {cycleResult.Error}", ConsoleColor.Yellow);
        }

        // ====================================================================
        // 4. Token 统计
        // ====================================================================
        statsCollector.PrintSummary();

        // ====================================================================
        // 5. 能力总结
        // ====================================================================
        ConsoleHelper.PrintDivider("📊 Handoff 能力总结");
        Console.WriteLine("  ✅ 支持 Agent 间任务转交");
        Console.WriteLine("  ✅ 自动解析 [HANDOFF:Agent] 格式");
        Console.WriteLine("  ✅ 完整的 Handoff 链路追踪");
        Console.WriteLine("  ✅ 循环检测防止无限递归");
        Console.WriteLine("  ✅ 可配置的最大深度限制");
        Console.WriteLine("  ✅ 超时控制和错误处理");
    }
}

/// <summary>
/// Handoff Token 统计收集器
/// </summary>
internal class HandoffTokenStats
{
    private readonly Dictionary<string, (int PromptTokens, int CompletionTokens, int CallCount)> _stats = [];

    public void Record(string agentName, int promptTokens, int completionTokens)
    {
        if (_stats.TryGetValue(agentName, out var current))
        {
            _stats[agentName] = (
                current.PromptTokens + promptTokens,
                current.CompletionTokens + completionTokens,
                current.CallCount + 1
            );
        }
        else
        {
            _stats[agentName] = (promptTokens, completionTokens, 1);
        }
    }

    public int TotalPromptTokens => _stats.Values.Sum(s => s.PromptTokens);
    public int TotalCompletionTokens => _stats.Values.Sum(s => s.CompletionTokens);
    public int TotalTokens => TotalPromptTokens + TotalCompletionTokens;
    public int TotalCallCount => _stats.Values.Sum(s => s.CallCount);

    public void PrintSummary()
    {
        ConsoleHelper.PrintDivider("📈 Token 使用统计");

        foreach (var (name, stats) in _stats.OrderByDescending(x => x.Value.PromptTokens + x.Value.CompletionTokens))
        {
            var total = stats.PromptTokens + stats.CompletionTokens;
            Console.WriteLine(
                $"  {name}: 输入={stats.PromptTokens}, 输出={stats.CompletionTokens}, 总计={total} ({stats.CallCount}次调用)"
            );
        }

        Console.WriteLine();
        ConsoleHelper.PrintColored(
            $"  📊 总计: 输入={TotalPromptTokens}, 输出={TotalCompletionTokens}, 总计={TotalTokens} ({TotalCallCount}次调用)",
            ConsoleColor.Yellow
        );
    }
}

/// <summary>
/// Triage Agent - 负责分析请求并分配给专家
/// </summary>
internal class TriageAgent : IHandoffAgent
{
    private readonly ILLMProvider _provider;
    private readonly HandoffTokenStats _stats;

    public TriageAgent(ILLMProvider provider, HandoffTokenStats stats)
    {
        _provider = provider;
        _stats = stats;
    }

    public string Name => "Triage";
    public string Instructions => "分析用户请求并分配给合适的专家";
    public IReadOnlyList<string> Handoffs => ["技术专家", "法律专家", "财务专家"];

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var startTime = DateTime.UtcNow;

        // 使用 LLM 分析用户请求并决定路由
        var systemPrompt = """
            你是一个智能分诊 Agent，负责分析用户请求并将其分配给合适的专家。

            可用的专家：
            - 技术专家：处理软件架构、系统设计、编程、DevOps 等技术问题
            - 法律专家：处理合同、协议、法规、合规等法律问题
            - 财务专家：处理预算、投资、财务分析、ROI 等财务问题

            请分析用户的问题，然后：
            1. 如果能明确分类，回复格式：[ROUTE:专家名称] 原因
            2. 如果无法分类，直接简短回答用户问题

            示例：
            - 用户问"如何设计微服务架构" → [ROUTE:技术专家] 这是软件架构设计问题
            - 用户问"合同需要注意什么" → [ROUTE:法律专家] 这是合同法律问题
            - 用户问"项目ROI如何计算" → [ROUTE:财务专家] 这是投资回报分析问题
            """;

        var messages = new List<ChatMessage>
        {
            new("system", systemPrompt),
            new("user", input),
        };

        var result = await _provider.ChatAsync(messages, cancellationToken: cancellationToken);
        var response = result.Content ?? "";
        var duration = DateTime.UtcNow - startTime;

        // 记录 Token 统计
        _stats.Record(Name, result.PromptTokens, result.CompletionTokens);

        // 解析 LLM 响应
        if (response.StartsWith("[ROUTE:"))
        {
            var endIndex = response.IndexOf(']');
            if (endIndex > 7)
            {
                var targetAgent = response.Substring(7, endIndex - 7);
                var reason = response.Length > endIndex + 1
                    ? response.Substring(endIndex + 1).Trim()
                    : "LLM 路由决策";

                var handoffResponse = AgentResponseHandoffExtensions.CreateHandoffResponse(
                    targetAgent,
                    input,
                    reason
                );

                return AgentResponse.Successful(handoffResponse, [], duration);
            }
        }

        // 无法分类，直接返回 LLM 的回答
        return AgentResponse.Successful(response, [], duration);
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}

/// <summary>
/// 专家 Agent - 处理特定领域问题
/// </summary>
internal class ExpertAgent : IAgent
{
    private readonly ILLMProvider _provider;
    private readonly HandoffTokenStats _stats;
    private readonly string _expertise;
    private readonly string _systemPrompt;

    public ExpertAgent(
        ILLMProvider provider,
        HandoffTokenStats stats,
        string name,
        string expertise,
        string systemPrompt
    )
    {
        _provider = provider;
        _stats = stats;
        Name = name;
        _expertise = expertise;
        _systemPrompt = systemPrompt;
    }

    public string Name { get; }
    public string Instructions => $"处理{_expertise}";

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var startTime = DateTime.UtcNow;

        var messages = new List<ChatMessage>
        {
            new("system", _systemPrompt),
            new("user", input),
        };

        var result = await _provider.ChatAsync(messages, cancellationToken: cancellationToken);
        var response = result.Content ?? "";
        var duration = DateTime.UtcNow - startTime;

        // 记录 Token 统计
        _stats.Record(Name, result.PromptTokens, result.CompletionTokens);

        return AgentResponse.Successful(response, [], duration);
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}

/// <summary>
/// 用于演示循环检测的 Agent
/// </summary>
internal class CyclicAgent : IAgent
{
    private readonly string _targetAgent;

    public CyclicAgent(string name, string targetAgent)
    {
        Name = name;
        _targetAgent = targetAgent;
    }

    public string Name { get; }
    public string Instructions => $"会 Handoff 到 {_targetAgent}";

    public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        var handoff = AgentResponseHandoffExtensions.CreateHandoffResponse(
            _targetAgent,
            input,
            "转交给另一个 Agent"
        );

        return Task.FromResult(
            AgentResponse.Successful(handoff, [], TimeSpan.FromMilliseconds(10))
        );
    }

    public Task<AgentResponse> RunAsync(
        AgentContext context,
        CancellationToken cancellationToken = default
    )
    {
        return RunAsync(context.UserInput, cancellationToken);
    }
}
