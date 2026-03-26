using Dawning.Agents.Abstractions.Distributed;
using Dawning.Agents.Abstractions.Tools;
using Dawning.Agents.Redis.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Dawning.Agents.Tests.Redis;

/// <summary>
/// Redis 工具使用追踪器测试
/// </summary>
public sealed class RedisToolUsageTrackerTests
{
    private readonly Mock<IConnectionMultiplexer> _connectionMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IServer> _serverMock;
    private readonly RedisOptions _options;
    private readonly RedisToolUsageTracker _tracker;

    public RedisToolUsageTrackerTests()
    {
        _connectionMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _serverMock = new Mock<IServer>();
        _options = new RedisOptions { InstanceName = "test:", DefaultDatabase = 0 };

        _connectionMock
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);
        _connectionMock
            .Setup(c => c.GetEndPoints(It.IsAny<bool>()))
            .Returns([new System.Net.DnsEndPoint("localhost", 6379)]);
        _connectionMock
            .Setup(c => c.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(_serverMock.Object);

        _tracker = new RedisToolUsageTracker(
            _connectionMock.Object,
            Options.Create(_options),
            NullLogger<RedisToolUsageTracker>.Instance
        );
    }

    [Fact]
    public async Task RecordUsageAsync_CreatesHashEntries()
    {
        // Arrange
        var batch = new Mock<IBatch>();
        _databaseMock.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        batch
            .Setup(b =>
                b.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.HashSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);
        _databaseMock
            .Setup(d =>
                d.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        var record = new ToolUsageRecord
        {
            ToolName = "test_tool",
            Success = true,
            Duration = TimeSpan.FromMilliseconds(100),
        };

        // Act
        await _tracker.RecordUsageAsync(record);

        // Assert
        batch.Verify(
            b =>
                b.HashIncrementAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains("tool_usage:test_tool")),
                    It.Is<RedisValue>(v => v.ToString() == "totalCalls"),
                    1,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsEmptyForUnknownTool()
    {
        // Arrange
        _databaseMock
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        // Act
        var stats = await _tracker.GetStatsAsync("unknown_tool");

        // Assert
        stats.ToolName.Should().Be("unknown_tool");
        stats.TotalCalls.Should().Be(0);
        stats.LastUsed.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsPopulatedStats()
    {
        // Arrange
        var lastUsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _databaseMock
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([
                new HashEntry("totalCalls", "10"),
                new HashEntry("successCount", "8"),
                new HashEntry("failureCount", "2"),
                new HashEntry("totalDurationMs", "5000"),
                new HashEntry("lastUsed", lastUsedMs.ToString()),
            ]);
        _databaseMock
            .Setup(d =>
                d.ListRangeAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync([new RedisValue("error1"), new RedisValue("error2")]);

        // Act
        var stats = await _tracker.GetStatsAsync("test_tool");

        // Assert
        stats.ToolName.Should().Be("test_tool");
        stats.TotalCalls.Should().Be(10);
        stats.SuccessCount.Should().Be(8);
        stats.FailureCount.Should().Be(2);
        stats.AverageLatency.Should().Be(TimeSpan.FromMilliseconds(500));
        stats.RecentErrors.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllStatsAsync_ScansAndReturnsAll()
    {
        // Arrange
        var keys = new RedisKey[]
        {
            new("test:tool_usage:tool1"),
            new("test:tool_usage:tool2"),
            new("test:tool_usage:tool1:errors"), // should be skipped
        };
        _serverMock
            .Setup(s =>
                s.Keys(
                    It.IsAny<int>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(keys);

        _databaseMock
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new HashEntry("totalCalls", "5")]);
        _databaseMock
            .Setup(d =>
                d.ListRangeAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync([]);

        // Act
        var allStats = await _tracker.GetAllStatsAsync();

        // Assert
        allStats.Should().HaveCount(2); // errors key should be filtered out
    }

    [Fact]
    public async Task RecordUsageAsync_ThrowsOnNull()
    {
        var act = () => _tracker.RecordUsageAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStatsAsync_ThrowsOnEmptyName()
    {
        var act = () => _tracker.GetStatsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullConnection()
    {
        var act = () => new RedisToolUsageTracker(null!, Options.Create(_options));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RecordUsageAsync_FailedRecordIncrementsFailureAndPushesError()
    {
        // Arrange
        var batch = new Mock<IBatch>();
        _databaseMock.Setup(d => d.CreateBatch(It.IsAny<object>())).Returns(batch.Object);

        batch
            .Setup(b =>
                b.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.HashSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);
        batch
            .Setup(b =>
                b.ListLeftPushAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);
        batch
            .Setup(b =>
                b.ListTrimAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .Returns(Task.CompletedTask);
        _databaseMock
            .Setup(d =>
                d.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(1);

        var record = new ToolUsageRecord
        {
            ToolName = "failing_tool",
            Success = false,
            Duration = TimeSpan.FromMilliseconds(50),
            ErrorMessage = "Something went wrong",
        };

        // Act
        await _tracker.RecordUsageAsync(record);

        // Assert
        batch.Verify(
            b =>
                b.HashIncrementAsync(
                    It.IsAny<RedisKey>(),
                    It.Is<RedisValue>(v => v.ToString() == "failureCount"),
                    1,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );

        batch.Verify(
            b =>
                b.ListLeftPushAsync(
                    It.Is<RedisKey>(k => k.ToString().Contains(":errors")),
                    It.Is<RedisValue>(v => v.ToString() == "Something went wrong"),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }
}
