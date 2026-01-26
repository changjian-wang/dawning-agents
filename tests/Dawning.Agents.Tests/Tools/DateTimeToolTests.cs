using Dawning.Agents.Core.Tools.BuiltIn;
using FluentAssertions;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// DateTimeTool 单元测试
/// </summary>
public class DateTimeToolTests
{
    private readonly DateTimeTool _tool = new();

    #region GetCurrentDateTime Tests

    [Fact]
    public void GetCurrentDateTime_NoFormat_ReturnsDefaultFormat()
    {
        var result = _tool.GetCurrentDateTime();

        // 应该返回 yyyy-MM-dd HH:mm:ss 格式
        result.Should().MatchRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
    }

    [Fact]
    public void GetCurrentDateTime_CustomFormat_ReturnsCustomFormat()
    {
        var result = _tool.GetCurrentDateTime("yyyy/MM/dd");

        result.Should().MatchRegex(@"\d{4}/\d{2}/\d{2}");
    }

    [Fact]
    public void GetCurrentDateTime_InvalidFormat_ReturnsErrorWithDefault()
    {
        var result = _tool.GetCurrentDateTime("invalid format %%%");

        result.Should().Contain("无效的日期格式");
        result.Should().Contain("使用默认格式");
    }

    [Fact]
    public void GetCurrentDateTime_EmptyFormat_ReturnsDefaultFormat()
    {
        var result = _tool.GetCurrentDateTime("");

        result.Should().MatchRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
    }

    #endregion

    #region GetUtcDateTime Tests

    [Fact]
    public void GetUtcDateTime_NoFormat_ReturnsIso8601()
    {
        var result = _tool.GetUtcDateTime();

        // ISO 8601 格式包含 T 和 时区信息
        result.Should().Contain("T");
    }

    [Fact]
    public void GetUtcDateTime_CustomFormat_ReturnsCustomFormat()
    {
        var result = _tool.GetUtcDateTime("yyyy-MM-dd");

        result.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}");
    }

    [Fact]
    public void GetUtcDateTime_InvalidFormat_ReturnsErrorWithIso()
    {
        var result = _tool.GetUtcDateTime("%%%invalid%%%");

        result.Should().Contain("无效的日期格式");
        result.Should().Contain("ISO 8601");
    }

    #endregion

    #region CalculateDateDiff Tests

    [Fact]
    public void CalculateDateDiff_ValidDates_ReturnsResult()
    {
        var result = _tool.CalculateDateDiff("2024-01-01", "2024-01-11");

        result.Should().Contain("10 天");
    }

    [Fact]
    public void CalculateDateDiff_NoEndDate_UsesCurrentDate()
    {
        var result = _tool.CalculateDateDiff("2020-01-01");

        result.Should().Contain("日期差");
        result.Should().Contain("天");
    }

    [Fact]
    public void CalculateDateDiff_InvalidStartDate_ReturnsError()
    {
        var result = _tool.CalculateDateDiff("invalid-date", "2024-01-01");

        result.Should().Contain("无法解析起始日期");
    }

    [Fact]
    public void CalculateDateDiff_InvalidEndDate_ReturnsError()
    {
        var result = _tool.CalculateDateDiff("2024-01-01", "invalid-date");

        result.Should().Contain("无法解析结束日期");
    }

    #endregion

    #region AddToDate Tests

    [Fact]
    public void AddToDate_AddDays_ReturnsResult()
    {
        var result = _tool.AddToDate("2024-01-01", days: 10);

        result.Should().Contain("2024-01-11");
    }

    [Fact]
    public void AddToDate_SubtractDays_ReturnsResult()
    {
        var result = _tool.AddToDate("2024-01-11", days: -10);

        result.Should().Contain("2024-01-01");
    }

    [Fact]
    public void AddToDate_AddHours_ReturnsResult()
    {
        var result = _tool.AddToDate("2024-01-01 00:00:00", hours: 5);

        result.Should().Contain("05:00:00");
    }

    [Fact]
    public void AddToDate_AddMinutes_ReturnsResult()
    {
        var result = _tool.AddToDate("2024-01-01 00:00:00", minutes: 30);

        result.Should().Contain("00:30:00");
    }

    [Fact]
    public void AddToDate_NoBaseDate_UsesCurrentDate()
    {
        var result = _tool.AddToDate(days: 1);

        result.Should().Contain("计算结果");
    }

    [Fact]
    public void AddToDate_InvalidBaseDate_ReturnsError()
    {
        var result = _tool.AddToDate("invalid-date", days: 1);

        result.Should().Contain("无法解析日期");
    }

    #endregion

    #region ParseDate Tests

    [Theory]
    [InlineData("2024-01-15")]
    [InlineData("2024/01/15")]
    [InlineData("January 15, 2024")]
    [InlineData("15 Jan 2024")]
    public void ParseDate_ValidDate_ReturnsStandardFormat(string dateString)
    {
        var result = _tool.ParseDate(dateString);

        result.Should().Contain("2024");
        result.Should().Contain("01");
        result.Should().Contain("15");
    }

    [Fact]
    public void ParseDate_InvalidDate_ReturnsError()
    {
        var result = _tool.ParseDate("not a date");

        result.Should().Contain("无法解析");
    }

    #endregion
}
