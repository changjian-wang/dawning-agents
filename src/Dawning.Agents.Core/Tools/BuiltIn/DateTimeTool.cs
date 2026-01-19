using Dawning.Agents.Abstractions.Tools;

namespace Dawning.Agents.Core.Tools.BuiltIn;

/// <summary>
/// 日期时间相关工具
/// </summary>
public class DateTimeTool
{
    /// <summary>
    /// 获取当前日期时间
    /// </summary>
    [FunctionTool("获取当前日期和时间，可指定格式", Category = "DateTime")]
    public string GetCurrentDateTime(
        [ToolParameter("日期时间格式，如 'yyyy-MM-dd HH:mm:ss'，留空使用默认格式")]
            string? format = null
    )
    {
        var now = DateTime.Now;
        if (string.IsNullOrWhiteSpace(format))
        {
            return now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        try
        {
            return now.ToString(format);
        }
        catch (FormatException)
        {
            return $"无效的日期格式: {format}，使用默认格式: {now:yyyy-MM-dd HH:mm:ss}";
        }
    }

    /// <summary>
    /// 获取当前 UTC 时间
    /// </summary>
    [FunctionTool("获取当前 UTC 日期和时间", Category = "DateTime")]
    public string GetUtcDateTime(
        [ToolParameter("日期时间格式，留空使用 ISO 8601 格式")] string? format = null
    )
    {
        var utcNow = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(format))
        {
            return utcNow.ToString("o"); // ISO 8601
        }

        try
        {
            return utcNow.ToString(format);
        }
        catch (FormatException)
        {
            return $"无效的日期格式: {format}，使用 ISO 8601: {utcNow:o}";
        }
    }

    /// <summary>
    /// 计算日期差
    /// </summary>
    [FunctionTool("计算两个日期之间的差值", Category = "DateTime")]
    public string CalculateDateDiff(
        [ToolParameter("起始日期，格式如 '2024-01-01'")] string startDate,
        [ToolParameter("结束日期，格式如 '2024-12-31'，留空使用当前日期")] string? endDate = null
    )
    {
        if (!DateTime.TryParse(startDate, out var start))
        {
            return $"无法解析起始日期: {startDate}";
        }

        DateTime end;
        if (string.IsNullOrWhiteSpace(endDate))
        {
            end = DateTime.Now;
        }
        else if (!DateTime.TryParse(endDate, out end))
        {
            return $"无法解析结束日期: {endDate}";
        }

        var diff = end - start;
        return $"日期差: {diff.Days} 天 ({diff.TotalHours:F1} 小时)";
    }

    /// <summary>
    /// 日期加减运算
    /// </summary>
    [FunctionTool("对日期进行加减运算", Category = "DateTime")]
    public string AddToDate(
        [ToolParameter("基准日期，格式如 '2024-01-01'，留空使用当前日期")] string? baseDate = null,
        [ToolParameter("要添加的天数（负数为减）")] int days = 0,
        [ToolParameter("要添加的小时数（负数为减）")] int hours = 0,
        [ToolParameter("要添加的分钟数（负数为减）")] int minutes = 0
    )
    {
        DateTime date;
        if (string.IsNullOrWhiteSpace(baseDate))
        {
            date = DateTime.Now;
        }
        else if (!DateTime.TryParse(baseDate, out date))
        {
            return $"无法解析日期: {baseDate}";
        }

        var result = date.AddDays(days).AddHours(hours).AddMinutes(minutes);
        return $"计算结果: {result:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// 解析日期
    /// </summary>
    [FunctionTool("解析日期字符串并返回标准格式", Category = "DateTime")]
    public string ParseDate([ToolParameter("要解析的日期字符串")] string dateString)
    {
        if (DateTime.TryParse(dateString, out var date))
        {
            return $"""
                解析成功:
                - 标准格式: {date:yyyy-MM-dd HH:mm:ss}
                - ISO 8601: {date:o}
                - 星期: {date.DayOfWeek}
                - 第 {date.DayOfYear} 天
                """;
        }

        return $"无法解析日期: {dateString}";
    }
}
