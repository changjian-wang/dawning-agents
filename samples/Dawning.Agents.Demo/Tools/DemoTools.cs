using System.Data;
using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Demo.Tools;

/// <summary>
/// 示例工具类 - 展示如何使用 [FunctionTool] 特性创建工具
/// </summary>
public class DemoTools
{
    /// <summary>
    /// 计算数学表达式
    /// </summary>
    [FunctionTool("计算数学表达式，支持加减乘除和括号")]
    public string Calculate(
        [ToolParameter("数学表达式，如 '2 + 3 * 4' 或 '(10 + 5) / 3'")] string expression
    )
    {
        try
        {
            // 使用 DataTable.Compute 进行简单数学计算
            var result = new DataTable().Compute(expression, null);
            return $"计算结果: {expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"计算错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 搜索信息（模拟）
    /// </summary>
    [FunctionTool("搜索相关信息和知识")]
    public string Search([ToolParameter("搜索关键词")] string query)
    {
        // 模拟搜索结果 - 实际应用中可集成真实搜索 API
        var results = query.ToLowerInvariant() switch
        {
            var q when q.Contains("agent") && q.Contains("架构") => """
                AI Agent 常见架构模式：
                1. 单体 Agent：一个 Agent 处理所有任务
                2. 专家型多 Agent：多个专精 Agent 各司其职
                3. 分层架构：协调者分配任务给执行者
                4. 代理-子代理：主 Agent 动态生成子 Agent
                """,
            var q when q.Contains("react") => """
                ReAct 模式是一种让 AI Agent 交替进行推理(Reasoning)和行动(Acting)的方法。
                核心循环：Thought → Action → Observation → Thought → ...
                优势：能根据实时反馈动态调整策略。
                """,
            var q when q.Contains("tool") || q.Contains("工具") => """
                AI Agent 常用工具类型：
                1. 搜索工具：检索信息和知识
                2. 计算工具：数学运算和数据处理
                3. 代码工具：执行代码和脚本
                4. API 工具：调用外部服务
                5. 文件工具：读写文件系统
                """,
            _ => $"搜索 '{query}' 的结果：这是一个模拟结果，实际应用中应集成真实搜索服务。",
        };

        return results;
    }

    /// <summary>
    /// 获取当前时间
    /// </summary>
    [FunctionTool("获取当前日期和时间")]
    public string GetCurrentTime()
    {
        return $"当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }
}
