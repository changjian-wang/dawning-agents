using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools;

/// <summary>
/// 默认工具选择器 - 基于关键词和分类匹配
/// </summary>
/// <remarks>
/// <para>简单实现：基于工具名称、描述和分类的关键词匹配</para>
/// <para>高级实现可使用 Embedding 进行语义匹配</para>
/// </remarks>
public class DefaultToolSelector : IToolSelector
{
    /// <summary>
    /// 根据查询选择最相关的工具
    /// </summary>
    public Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query,
        IReadOnlyList<ITool> availableTools,
        int maxTools = 20,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));
        ArgumentNullException.ThrowIfNull(availableTools, nameof(availableTools));

        if (availableTools.Count <= maxTools)
        {
            return Task.FromResult(availableTools);
        }

        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scoredTools = availableTools
            .Select(tool => (Tool: tool, Score: CalculateToolScore(tool, queryLower, queryWords)))
            .OrderByDescending(x => x.Score)
            .Take(maxTools)
            .Select(x => x.Tool)
            .ToList();

        return Task.FromResult<IReadOnlyList<ITool>>(scoredTools);
    }

    /// <summary>
    /// 根据查询选择最相关的工具集
    /// </summary>
    public Task<IReadOnlyList<IToolSet>> SelectToolSetsAsync(
        string query,
        IReadOnlyList<IToolSet> availableToolSets,
        int maxToolSets = 5,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));
        ArgumentNullException.ThrowIfNull(availableToolSets, nameof(availableToolSets));

        if (availableToolSets.Count <= maxToolSets)
        {
            return Task.FromResult(availableToolSets);
        }

        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scoredToolSets = availableToolSets
            .Select(ts => (ToolSet: ts, Score: CalculateToolSetScore(ts, queryLower, queryWords)))
            .OrderByDescending(x => x.Score)
            .Take(maxToolSets)
            .Select(x => x.ToolSet)
            .ToList();

        return Task.FromResult<IReadOnlyList<IToolSet>>(scoredToolSets);
    }

    private static double CalculateToolScore(ITool tool, string queryLower, string[] queryWords)
    {
        double score = 0;
        var nameLower = tool.Name.ToLowerInvariant();
        var descLower = tool.Description.ToLowerInvariant();
        var categoryLower = tool.Category?.ToLowerInvariant() ?? "";

        // 完全匹配工具名称 - 最高分
        if (nameLower == queryLower || queryLower.Contains(nameLower))
        {
            score += 10;
        }

        // 名称包含查询词
        foreach (var word in queryWords)
        {
            if (nameLower.Contains(word))
            {
                score += 5;
            }

            if (descLower.Contains(word))
            {
                score += 2;
            }

            if (categoryLower.Contains(word))
            {
                score += 3;
            }
        }

        // 关键词匹配（常见操作词）
        score += MatchKeywords(queryLower, tool);

        return score;
    }

    private static double CalculateToolSetScore(
        IToolSet toolSet,
        string queryLower,
        string[] queryWords
    )
    {
        double score = 0;
        var nameLower = toolSet.Name.ToLowerInvariant();
        var descLower = toolSet.Description.ToLowerInvariant();

        // 工具集名称匹配
        if (nameLower == queryLower || queryLower.Contains(nameLower))
        {
            score += 10;
        }

        foreach (var word in queryWords)
        {
            if (nameLower.Contains(word))
            {
                score += 5;
            }

            if (descLower.Contains(word))
            {
                score += 2;
            }
        }

        // 累加内部工具的匹配分数
        foreach (var tool in toolSet.Tools)
        {
            var toolScore = CalculateToolScore(tool, queryLower, queryWords);
            score += toolScore * 0.5; // 内部工具分数权重较低
        }

        return score;
    }

    private static double MatchKeywords(string query, ITool tool)
    {
        double score = 0;
        var nameLower = tool.Name.ToLowerInvariant();
        var categoryLower = tool.Category?.ToLowerInvariant() ?? "";

        // 文件操作相关
        if (
            (query.Contains("file") || query.Contains("文件"))
            && (categoryLower.Contains("filesystem") || nameLower.Contains("file"))
        )
        {
            score += 3;
        }

        // 搜索相关
        if (
            (
                query.Contains("search")
                || query.Contains("搜索")
                || query.Contains("find")
                || query.Contains("查找")
            )
            && (
                nameLower.Contains("search")
                || nameLower.Contains("grep")
                || nameLower.Contains("find")
            )
        )
        {
            score += 3;
        }

        // Git 相关
        if (
            (
                query.Contains("git")
                || query.Contains("commit")
                || query.Contains("push")
                || query.Contains("提交")
            ) && categoryLower.Contains("git")
        )
        {
            score += 3;
        }

        // HTTP 相关
        if (
            (
                query.Contains("http")
                || query.Contains("api")
                || query.Contains("request")
                || query.Contains("请求")
            ) && categoryLower.Contains("http")
        )
        {
            score += 3;
        }

        // 计算相关
        if (
            (query.Contains("calc") || query.Contains("math") || query.Contains("计算"))
            && categoryLower.Contains("math")
        )
        {
            score += 3;
        }

        // 时间相关
        if (
            (
                query.Contains("time")
                || query.Contains("date")
                || query.Contains("时间")
                || query.Contains("日期")
            ) && categoryLower.Contains("datetime")
        )
        {
            score += 3;
        }

        return score;
    }
}
