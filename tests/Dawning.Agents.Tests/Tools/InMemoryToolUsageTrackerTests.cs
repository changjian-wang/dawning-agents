using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Core.Tools;
using FluentAssertions;
using Xunit;

namespace Dawning.Agents.Tests.Tools;

/// <summary>
/// InMemoryToolUsageTracker 测试
/// </summary>
public sealed class InMemoryToolUsageTrackerTests
{
    private readonly InMemoryToolUsageTracker _tracker = new();

    #region RecordUsageAsync

    [Fact]
    public async Task RecordUsageAsync_ShouldAcceptValidRecord()
    {
        var record = new ToolUsageRecord
        {
            ToolName = "test_tool",
            Success = true,
            Duration = TimeSpan.FromMilliseconds(100),
        };

        await _tracker.RecordUsageAsync(record);

        var stats = await _tracker.GetStatsAsync("test_tool");
        stats.TotalCalls.Should().Be(1);
        stats.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordUsageAsync_NullRecord_ShouldThrow()
    {
        var act = () => _tracker.RecordUsageAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RecordUsageAsync_MultipleRecords_ShouldAccumulate()
    {
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(100),
            }
        );
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = false,
                Duration = TimeSpan.FromMilliseconds(200),
                ErrorMessage = "fail",
            }
        );

        var stats = await _tracker.GetStatsAsync("tool");
        stats.TotalCalls.Should().Be(2);
        stats.SuccessCount.Should().Be(1);
        stats.FailureCount.Should().Be(1);
        stats.SuccessRate.Should().Be(0.5f);
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldTrackAverageLatency()
    {
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(100),
            }
        );
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(300),
            }
        );

        var stats = await _tracker.GetStatsAsync("tool");
        stats.AverageLatency.TotalMilliseconds.Should().Be(200);
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldTrackRecentErrors()
    {
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = false,
                ErrorMessage = "error1",
            }
        );
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = false,
                ErrorMessage = "error2",
            }
        );

        var stats = await _tracker.GetStatsAsync("tool");
        stats.RecentErrors.Should().HaveCount(2);
        stats.RecentErrors.Should().Contain("error1").And.Contain("error2");
    }

    [Fact]
    public async Task RecordUsageAsync_ShouldCapRecentErrors()
    {
        var tracker = new InMemoryToolUsageTracker(maxRecentErrors: 2);

        for (var i = 0; i < 5; i++)
        {
            await tracker.RecordUsageAsync(
                new ToolUsageRecord
                {
                    ToolName = "tool",
                    Success = false,
                    ErrorMessage = $"error{i}",
                }
            );
        }

        var stats = await tracker.GetStatsAsync("tool");
        stats.RecentErrors.Should().HaveCount(2);
        stats.RecentErrors.Should().Contain("error3").And.Contain("error4");
    }

    #endregion

    #region GetStatsAsync

    [Fact]
    public async Task GetStatsAsync_UnknownTool_ShouldReturnEmpty()
    {
        var stats = await _tracker.GetStatsAsync("unknown");
        stats.ToolName.Should().Be("unknown");
        stats.TotalCalls.Should().Be(0);
        stats.SuccessRate.Should().Be(0f);
    }

    [Fact]
    public async Task GetStatsAsync_NullToolName_ShouldThrow()
    {
        var act = () => _tracker.GetStatsAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetStatsAsync_EmptyToolName_ShouldThrow()
    {
        var act = () => _tracker.GetStatsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetAllStatsAsync

    [Fact]
    public async Task GetAllStatsAsync_Empty_ShouldReturnEmptyList()
    {
        var stats = await _tracker.GetAllStatsAsync();
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllStatsAsync_MultipleTools_ShouldReturnAll()
    {
        await _tracker.RecordUsageAsync(new ToolUsageRecord { ToolName = "a", Success = true });
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "b",
                Success = false,
                ErrorMessage = "err",
            }
        );

        var stats = await _tracker.GetAllStatsAsync();
        stats.Should().HaveCount(2);
        stats.Select(s => s.ToolName).Should().Contain("a").And.Contain("b");
    }

    #endregion

    #region GetLowUtilityToolsAsync

    [Fact]
    public async Task GetLowUtilityToolsAsync_ShouldFilterByThreshold()
    {
        // Tool with 100% success
        for (var i = 0; i < 5; i++)
        {
            await _tracker.RecordUsageAsync(
                new ToolUsageRecord { ToolName = "good", Success = true }
            );
        }

        // Tool with 0% success
        for (var i = 0; i < 5; i++)
        {
            await _tracker.RecordUsageAsync(
                new ToolUsageRecord
                {
                    ToolName = "bad",
                    Success = false,
                    ErrorMessage = "fail",
                }
            );
        }

        var lowUtility = await _tracker.GetLowUtilityToolsAsync(0.3f, 3);
        lowUtility.Should().HaveCount(1);
        lowUtility[0].ToolName.Should().Be("bad");
    }

    [Fact]
    public async Task GetLowUtilityToolsAsync_ShouldRespectMinCalls()
    {
        // Only 2 calls (below minCalls=3 threshold)
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = false,
                ErrorMessage = "fail",
            }
        );
        await _tracker.RecordUsageAsync(
            new ToolUsageRecord
            {
                ToolName = "tool",
                Success = false,
                ErrorMessage = "fail",
            }
        );

        var lowUtility = await _tracker.GetLowUtilityToolsAsync(0.3f, 3);
        lowUtility.Should().BeEmpty();
    }

    #endregion

    #region ToolUsageStats Record

    [Fact]
    public void ToolUsageStats_SuccessRate_ZeroCalls_ShouldReturnZero()
    {
        var stats = new ToolUsageStats { ToolName = "x" };
        stats.SuccessRate.Should().Be(0f);
    }

    [Fact]
    public void ToolUsageStats_SuccessRate_ShouldCalculateCorrectly()
    {
        var stats = new ToolUsageStats
        {
            ToolName = "x",
            TotalCalls = 10,
            SuccessCount = 7,
            FailureCount = 3,
        };
        stats.SuccessRate.Should().BeApproximately(0.7f, 0.001f);
    }

    #endregion
}
