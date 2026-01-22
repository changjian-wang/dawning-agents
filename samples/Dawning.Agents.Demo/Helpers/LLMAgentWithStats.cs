using Dawning.Agents.Abstractions.Agent;
using Dawning.Agents.Abstractions.LLM;
using Dawning.Agents.Core.LLM;

namespace Dawning.Agents.Demo.Helpers;

/// <summary>
/// 带 Token 统计的 LLM Agent
/// </summary>
public class LLMAgentWithStats : IAgent
{
    private readonly ILLMProvider _provider;
    private readonly string _systemPrompt;

    // Token 统计
    private int _totalPromptTokens;
    private int _totalCompletionTokens;
    private int _callCount;

    public LLMAgentWithStats(ILLMProvider provider, string name, string systemPrompt)
    {
        _provider = provider;
        Name = name;
        _systemPrompt = systemPrompt;
    }

    public string Name { get; }
    public string Instructions => _systemPrompt;

    /// <summary>
    /// 最近一次调用的输入 Token 数
    /// </summary>
    public int LastPromptTokens { get; private set; }

    /// <summary>
    /// 最近一次调用的输出 Token 数
    /// </summary>
    public int LastCompletionTokens { get; private set; }

    /// <summary>
    /// 最近一次调用的总 Token 数
    /// </summary>
    public int LastTotalTokens => LastPromptTokens + LastCompletionTokens;

    /// <summary>
    /// 累计输入 Token 数
    /// </summary>
    public int TotalPromptTokens => _totalPromptTokens;

    /// <summary>
    /// 累计输出 Token 数
    /// </summary>
    public int TotalCompletionTokens => _totalCompletionTokens;

    /// <summary>
    /// 累计总 Token 数
    /// </summary>
    public int TotalTokens => _totalPromptTokens + _totalCompletionTokens;

    /// <summary>
    /// 调用次数
    /// </summary>
    public int CallCount => _callCount;

    /// <summary>
    /// 获取 Token 统计字符串
    /// </summary>
    public string GetLastTokenStats() =>
        $"Token: 输入={LastPromptTokens}, 输出={LastCompletionTokens}, 总计={LastTotalTokens}";

    /// <summary>
    /// 获取累计统计字符串
    /// </summary>
    public string GetTotalStats() =>
        $"累计 Token: 输入={TotalPromptTokens}, 输出={TotalCompletionTokens}, 总计={TotalTokens}, 调用次数={CallCount}";

    /// <summary>
    /// 重置统计
    /// </summary>
    public void ResetStats()
    {
        _totalPromptTokens = 0;
        _totalCompletionTokens = 0;
        _callCount = 0;
        LastPromptTokens = 0;
        LastCompletionTokens = 0;
    }

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var messages = new List<ChatMessage>
            {
                new("system", _systemPrompt),
                new("user", input),
            };

            var result = await _provider.ChatAsync(messages, cancellationToken: cancellationToken);
            stopwatch.Stop();

            // 更新统计
            LastPromptTokens = result.PromptTokens;
            LastCompletionTokens = result.CompletionTokens;
            _totalPromptTokens += result.PromptTokens;
            _totalCompletionTokens += result.CompletionTokens;
            _callCount++;

            return AgentResponse.Successful(result.Content ?? "", [], stopwatch.Elapsed);
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

/// <summary>
/// Token 统计收集器 - 用于跟踪多个 Agent 的 Token 使用情况
/// </summary>
public class TokenStatsCollector
{
    private readonly List<LLMAgentWithStats> _agents = [];

    public void Register(LLMAgentWithStats agent)
    {
        _agents.Add(agent);
    }

    public void RegisterRange(IEnumerable<LLMAgentWithStats> agents)
    {
        _agents.AddRange(agents);
    }

    public int TotalPromptTokens => _agents.Sum(a => a.TotalPromptTokens);
    public int TotalCompletionTokens => _agents.Sum(a => a.TotalCompletionTokens);
    public int TotalTokens => TotalPromptTokens + TotalCompletionTokens;
    public int TotalCallCount => _agents.Sum(a => a.CallCount);

    public void PrintSummary()
    {
        ConsoleHelper.PrintDivider("📈 Token 使用统计");

        foreach (var agent in _agents.Where(a => a.CallCount > 0))
        {
            Console.WriteLine(
                $"  {agent.Name}: 输入={agent.TotalPromptTokens}, 输出={agent.TotalCompletionTokens}, 总计={agent.TotalTokens} ({agent.CallCount}次调用)"
            );
        }

        Console.WriteLine();
        ConsoleHelper.PrintColored(
            $"  📊 总计: 输入={TotalPromptTokens}, 输出={TotalCompletionTokens}, 总计={TotalTokens} ({TotalCallCount}次调用)",
            ConsoleColor.Yellow
        );
    }

    public void Reset()
    {
        foreach (var agent in _agents)
        {
            agent.ResetStats();
        }
    }
}
