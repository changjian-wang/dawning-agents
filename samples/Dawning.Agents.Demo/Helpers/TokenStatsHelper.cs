using Dawning.Agents.Abstractions.Telemetry;
using Dawning.Agents.Core.Telemetry;

namespace Dawning.Agents.Demo.Helpers;

/// <summary>
/// Token 统计显示助手 - 用于演示时显示 Token 使用情况
/// </summary>
public static class TokenStatsHelper
{
    /// <summary>
    /// 打印 Token 统计摘要
    /// </summary>
    public static void PrintSummary(ITokenUsageTracker tracker)
    {
        ConsoleHelper.PrintDivider("📈 Token 使用统计");

        var summary = tracker.GetSummary();

        foreach (
            var (source, usage) in summary.BySource.OrderByDescending(x => x.Value.TotalTokens)
        )
        {
            Console.WriteLine(
                $"  {source}: 输入={usage.PromptTokens}, 输出={usage.CompletionTokens}, 总计={usage.TotalTokens} ({usage.CallCount}次调用)"
            );
        }

        Console.WriteLine();
        ConsoleHelper.PrintColored(
            $"  📊 总计: 输入={summary.TotalPromptTokens}, 输出={summary.TotalCompletionTokens}, 总计={summary.TotalTokens} ({summary.CallCount}次调用)",
            ConsoleColor.Yellow
        );
    }

    /// <summary>
    /// 创建新的内存 Token 追踪器
    /// </summary>
    public static InMemoryTokenUsageTracker CreateTracker() => new();
}
